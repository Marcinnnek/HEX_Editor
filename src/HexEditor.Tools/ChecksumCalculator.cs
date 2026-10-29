using System.Globalization;
using HexEditor.Core;

namespace HexEditor.Tools;

public enum ChecksumKind
{
    Additive,
    Crc
}

public enum MissingAddressTreatment
{
    Reject,
    FillByte
}

public enum ChecksumByteOrder
{
    BigEndian,
    LittleEndian
}

public sealed record ChecksumRequest
{
    private ChecksumRequest(
        uint startAddress,
        uint endAddress,
        ChecksumKind kind,
        int widthBits,
        ulong polynomial,
        ulong initialValue,
        bool reflectInput,
        bool reflectOutput,
        ulong finalXor,
        MissingAddressTreatment missingAddressTreatment,
        byte? missingAddressFillByte,
        ChecksumByteOrder byteOrder,
        string algorithmName)
    {
        if (endAddress < startAddress)
            throw new ArgumentOutOfRangeException(nameof(endAddress), "The range end must not precede its start.");
        if (widthBits is not (8 or 16 or 32))
            throw new ArgumentOutOfRangeException(nameof(widthBits), "Checksum width must be 8, 16, or 32 bits.");
        if (!Enum.IsDefined(missingAddressTreatment))
            throw new ArgumentOutOfRangeException(nameof(missingAddressTreatment));
        if (!Enum.IsDefined(byteOrder))
            throw new ArgumentOutOfRangeException(nameof(byteOrder));
        if (missingAddressTreatment == MissingAddressTreatment.FillByte && missingAddressFillByte is null)
            throw new ArgumentException("Choose a fill byte when missing addresses are treated as filled.", nameof(missingAddressFillByte));
        if (missingAddressTreatment == MissingAddressTreatment.Reject && missingAddressFillByte is not null)
            throw new ArgumentException("A fill byte is only valid when missing addresses are filled.", nameof(missingAddressFillByte));

        var mask = Mask(widthBits);
        if (kind == ChecksumKind.Crc)
        {
            if (polynomial == 0 || (polynomial & ~mask) != 0)
                throw new ArgumentOutOfRangeException(nameof(polynomial), "The CRC polynomial must be non-zero and fit the selected width.");
            if ((initialValue & ~mask) != 0)
                throw new ArgumentOutOfRangeException(nameof(initialValue), "The CRC initial value must fit the selected width.");
            if ((finalXor & ~mask) != 0)
                throw new ArgumentOutOfRangeException(nameof(finalXor), "The CRC final XOR must fit the selected width.");
        }

        StartAddress = startAddress;
        EndAddress = endAddress;
        Kind = kind;
        WidthBits = widthBits;
        Polynomial = polynomial;
        InitialValue = initialValue;
        ReflectInput = reflectInput;
        ReflectOutput = reflectOutput;
        FinalXor = finalXor;
        MissingAddressTreatment = missingAddressTreatment;
        MissingAddressFillByte = missingAddressFillByte;
        ByteOrder = byteOrder;
        AlgorithmName = algorithmName;
    }

    public uint StartAddress { get; }
    public uint EndAddress { get; }
    public ChecksumKind Kind { get; }
    public int WidthBits { get; }
    public ulong Polynomial { get; }
    public ulong InitialValue { get; }
    public bool ReflectInput { get; }
    public bool ReflectOutput { get; }
    public ulong FinalXor { get; }
    public MissingAddressTreatment MissingAddressTreatment { get; }
    public byte? MissingAddressFillByte { get; }
    public ChecksumByteOrder ByteOrder { get; }
    public string AlgorithmName { get; }

    public static ChecksumRequest Additive(
        uint startAddress,
        uint endAddress,
        int widthBits,
        MissingAddressTreatment missingAddressTreatment,
        byte? missingAddressFillByte = null,
        ChecksumByteOrder byteOrder = ChecksumByteOrder.BigEndian) =>
        new(startAddress, endAddress, ChecksumKind.Additive, widthBits, 0, 0, false, false, 0,
            missingAddressTreatment, missingAddressFillByte, byteOrder, $"Additive {widthBits}-bit");

    public static ChecksumRequest Crc16CcittFalse(
        uint startAddress,
        uint endAddress,
        MissingAddressTreatment missingAddressTreatment,
        byte? missingAddressFillByte = null,
        ChecksumByteOrder byteOrder = ChecksumByteOrder.BigEndian) =>
        CustomCrc(startAddress, endAddress, 16, 0x1021, 0xFFFF, false, false, 0,
            missingAddressTreatment, missingAddressFillByte, byteOrder, "CRC-16/CCITT-FALSE");

    public static ChecksumRequest Crc32IsoHdlc(
        uint startAddress,
        uint endAddress,
        MissingAddressTreatment missingAddressTreatment,
        byte? missingAddressFillByte = null,
        ChecksumByteOrder byteOrder = ChecksumByteOrder.BigEndian) =>
        CustomCrc(startAddress, endAddress, 32, 0x04C11DB7, 0xFFFFFFFF, true, true, 0xFFFFFFFF,
            missingAddressTreatment, missingAddressFillByte, byteOrder, "CRC-32/ISO-HDLC");

    public static ChecksumRequest CustomCrc(
        uint startAddress,
        uint endAddress,
        int widthBits,
        ulong polynomial,
        ulong initialValue,
        bool reflectInput,
        bool reflectOutput,
        ulong finalXor,
        MissingAddressTreatment missingAddressTreatment,
        byte? missingAddressFillByte = null,
        ChecksumByteOrder byteOrder = ChecksumByteOrder.BigEndian,
        string algorithmName = "Custom CRC") =>
        new(startAddress, endAddress, ChecksumKind.Crc, widthBits, polynomial, initialValue,
            reflectInput, reflectOutput, finalXor, missingAddressTreatment, missingAddressFillByte,
            byteOrder, algorithmName);

    internal static ulong Mask(int widthBits) => widthBits == 32 ? uint.MaxValue : (1UL << widthBits) - 1;
}

public sealed record ChecksumCalculationResult(
    uint StartAddress,
    uint EndAddress,
    ChecksumKind Kind,
    string AlgorithmName,
    int WidthBits,
    ulong Value,
    string Parameters,
    ChecksumByteOrder ByteOrder)
{
    public bool IsStale { get; init; }

    public string FormattedValue => $"0x{Value.ToString($"X{WidthBits / 4}", CultureInfo.InvariantCulture)}";

    public byte[] ToBytes()
    {
        var count = WidthBits / 8;
        var bytes = new byte[count];
        for (var index = 0; index < count; index++)
        {
            var shift = ByteOrder == ChecksumByteOrder.BigEndian
                ? (count - index - 1) * 8
                : index * 8;
            bytes[index] = (byte)(Value >> shift);
        }
        return bytes;
    }
}

/// <summary>Calculates reproducible checksums over absolute-address ranges in a sparse memory image.</summary>
public static class ChecksumCalculator
{
    public static ChecksumCalculationResult Calculate(MemoryImage image, ChecksumRequest request)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(request);

        var mask = ChecksumRequest.Mask(request.WidthBits);
        var value = request.Kind == ChecksumKind.Additive ? 0 : request.InitialValue;
        for (ulong address = request.StartAddress; address <= request.EndAddress; address++)
        {
            if (!image.TryGetByte((uint)address, out var currentByte))
            {
                if (request.MissingAddressTreatment == MissingAddressTreatment.Reject)
                    throw new InvalidOperationException($"Address 0x{address:X8} is missing from the selected range.");
                currentByte = request.MissingAddressFillByte!.Value;
            }

            value = request.Kind == ChecksumKind.Additive
                ? (value + currentByte) & mask
                : UpdateCrc(value, currentByte, request, mask);
        }

        if (request.Kind == ChecksumKind.Crc)
        {
            if (request.ReflectInput != request.ReflectOutput)
                value = Reflect(value, request.WidthBits);
            value = (value ^ request.FinalXor) & mask;
        }

        return new ChecksumCalculationResult(
            request.StartAddress,
            request.EndAddress,
            request.Kind,
            request.AlgorithmName,
            request.WidthBits,
            value,
            DescribeParameters(request),
            request.ByteOrder);
    }

    private static ulong UpdateCrc(ulong crc, byte input, ChecksumRequest request, ulong mask)
    {
        if (request.ReflectInput)
        {
            crc ^= input;
            var reflectedPolynomial = Reflect(request.Polynomial, request.WidthBits);
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ reflectedPolynomial : crc >> 1;
            return crc & mask;
        }

        crc ^= (ulong)input << (request.WidthBits - 8);
        var topBit = 1UL << (request.WidthBits - 1);
        for (var bit = 0; bit < 8; bit++)
            crc = (crc & topBit) != 0 ? ((crc << 1) ^ request.Polynomial) & mask : (crc << 1) & mask;
        return crc;
    }

    private static ulong Reflect(ulong value, int width)
    {
        ulong reflected = 0;
        for (var bit = 0; bit < width; bit++)
        {
            reflected = (reflected << 1) | (value & 1);
            value >>= 1;
        }
        return reflected;
    }

    private static string DescribeParameters(ChecksumRequest request)
    {
        var missing = request.MissingAddressTreatment == MissingAddressTreatment.Reject
            ? "reject holes"
            : $"fill 0x{request.MissingAddressFillByte:X2}";
        if (request.Kind == ChecksumKind.Additive)
            return $"width={request.WidthBits}; missing={missing}; order={request.ByteOrder}";

        return $"width={request.WidthBits}; poly=0x{request.Polynomial:X}; init=0x{request.InitialValue:X}; " +
               $"refin={request.ReflectInput.ToString().ToLowerInvariant()}; " +
               $"refout={request.ReflectOutput.ToString().ToLowerInvariant()}; xorout=0x{request.FinalXor:X}; " +
               $"missing={missing}; order={request.ByteOrder}";
    }
}
