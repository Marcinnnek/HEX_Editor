namespace HexEditor.UI;

/// <summary>Keeps the cursor and navigation decisions for one open Compare view.</summary>
internal sealed class CompareNavigationSession(CompareViewModel comparison)
{
    private uint? _selectedByteAddress;
    private uint? _rowWidthChangeAddress;
    private bool _rowWidthChangeHadByteSelection;
    private ComparePanelSide _rowWidthChangeSide = ComparePanelSide.Base;

    public ComparePanelSide Side { get; private set; } = ComparePanelSide.Base;
    public string AddressText { get; private set; } = string.Empty;

    public void SetAddressDraft(string text) => AddressText = text;

    public void FocusSide(ComparePanelSide side) => Side = side;

    public void SelectByte(ComparePanelSide side, uint address)
    {
        Side = side;
        _selectedByteAddress = address;
        AddressText = address.ToString("X8");
    }

    public void SelectAddressRow(ComparePanelSide side, uint? rowAddress)
    {
        Side = side;
        _selectedByteAddress = null;
        if (rowAddress is uint address) AddressText = address.ToString("X8");
    }

    public void ObserveGridRow(ComparePanelSide side, uint? rowAddress)
    {
        Side = side;
        if (rowAddress is uint address) AddressText = address.ToString("X8");
    }

    public void ScrollToFirstRow(uint? rowAddress)
    {
        _selectedByteAddress = null;
        if (rowAddress is uint address) AddressText = address.ToString("X8");
    }

    public uint? CurrentAddress(uint? visibleRowAddress, int bytesPerRow, bool windowed)
    {
        if (windowed && _selectedByteAddress is uint selectedByte) return selectedByte;
        if (visibleRowAddress is not uint rowAddress) return null;
        if (HexAddressParser.TryParse(AddressText, out var address) &&
            (ulong)address >= rowAddress && (ulong)address < (ulong)rowAddress + (uint)bytesPerRow)
            return address;
        return rowAddress;
    }

    public (ComparePanelSide Side, uint Address)? CurrentCursor(uint? visibleRowAddress, int bytesPerRow, bool windowed) =>
        CurrentAddress(visibleRowAddress, bytesPerRow, windowed) is uint address ? (Side, address) : null;

    public bool TryGo(out int rowIndex)
    {
        rowIndex = -1;
        if (!HexAddressParser.TryParse(AddressText, out var address) ||
            !comparison.TryNavigateToAddress(address, out rowIndex))
            return false;
        _selectedByteAddress = address;
        return true;
    }

    public bool TryNavigate(uint address, out int rowIndex)
    {
        if (!comparison.TryNavigateToAddress(address, out rowIndex)) return false;
        SelectByte(Side, address);
        return true;
    }

    public bool Navigate(uint address, out int rowIndex)
    {
        SelectByte(Side, address);
        return comparison.TryNavigateToAddress(address, out rowIndex);
    }

    public bool NavigateToFindMatch(ComparePanelSide side, uint start, uint end, out int rowIndex)
    {
        SelectByte(side, start);
        comparison.ClearFindMatch();
        comparison.SetFindMatch(side == ComparePanelSide.Base, start, end);
        return comparison.TryNavigateToAddress(start, out rowIndex);
    }

    public CompareDifferenceNavigationResult NavigateDifference(bool forward, uint? visibleRowAddress,
        int bytesPerRow, bool windowed)
    {
        var selected = CurrentAddress(visibleRowAddress, bytesPerRow, windowed);
        var hasText = !string.IsNullOrWhiteSpace(AddressText);
        var hasAnchor = HexAddressParser.TryParse(AddressText, out var parsedAddress);
        if (selected is null && hasText && !hasAnchor)
            return new CompareDifferenceNavigationResult(CompareDifferenceNavigationKind.InvalidAddress);

        uint? anchor = hasAnchor ? parsedAddress : null;
        var found = forward
            ? comparison.TryFindNextDifferenceFromAddress(selected, anchor, out var address)
            : comparison.TryFindPreviousDifferenceFromAddress(selected, anchor, out address);
        if (!found)
            return new CompareDifferenceNavigationResult(CompareDifferenceNavigationKind.NoDifference);

        var navigated = Navigate(address, out var rowIndex);
        return new CompareDifferenceNavigationResult(CompareDifferenceNavigationKind.Found,
            navigated ? rowIndex : -1);
    }

    public uint? DifferenceContext(uint? visibleRowAddress, int bytesPerRow, bool windowed)
    {
        var selected = CurrentAddress(visibleRowAddress, bytesPerRow, windowed);
        return selected ?? (HexAddressParser.TryParse(AddressText, out var address) ? address : null);
    }

    public void BeginRowWidthChange(uint? visibleRowAddress, int bytesPerRow, bool windowed)
    {
        _rowWidthChangeAddress = CurrentAddress(visibleRowAddress, bytesPerRow, windowed);
        _rowWidthChangeHadByteSelection = _selectedByteAddress is not null;
        _rowWidthChangeSide = Side;
    }

    public bool FinishRowWidthChange(out int rowIndex)
    {
        var address = _rowWidthChangeAddress;
        var hadByteSelection = _rowWidthChangeHadByteSelection;
        Side = _rowWidthChangeSide;
        _rowWidthChangeAddress = null;
        _rowWidthChangeHadByteSelection = false;

        rowIndex = -1;
        if (address is not uint selectedAddress ||
            !comparison.TryNavigateToAddress(selectedAddress, out rowIndex))
            return false;
        if (hadByteSelection) _selectedByteAddress = selectedAddress;
        return true;
    }
}

internal enum CompareDifferenceNavigationKind { Found, InvalidAddress, NoDifference }
internal readonly record struct CompareDifferenceNavigationResult(CompareDifferenceNavigationKind Kind, int RowIndex = -1);
