using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Lumina.Data;
using Lumina.Data.Files.Excel;
using Lumina.Data.Structs.Excel;

namespace Lumina.Excel;

public sealed partial class ExcelSheet< T >
    : IExcelSheet, IDictionary, IDictionary< uint, T >, IReadOnlyDictionary< uint, T > where T : struct, IExcelRow< T >
{
    /// <inheritdoc/>
    public ExcelModule Module { get; }

    /// <inheritdoc/>
    public Language Language { get; }

    private List< ExcelPage > Pages { get; }

    private ImmutableArray< RowLookup > Lookup { get; }

    private ushort SubrowDataOffset { get; }

    /// <summary>
    /// Gets a value indicating whether this sheet has subrows, where each row id can have multiple subrows.
    /// </summary>
    public bool HasSubrows { get; }

    private static SheetAttribute Attribute =>
        typeof( T ).GetCustomAttribute< SheetAttribute >() ??
        throw new InvalidOperationException( "T has no SheetAttribute. Use the explicit sheet constructor." );

    /// <summary>
    /// The number of rows in this sheet.
    /// </summary>
    /// <remarks>
    /// If this sheet has gaps in row ids, it returns the number of rows that exist, not the highest row id.
    /// If this sheet has subrows, this will still return the number of rows and not the total number of subrows.
    /// </remarks>
    public int Count => Lookup.Length;

    /// <summary>
    /// Create an <see cref="ExcelSheet{T}"/> instance with the <paramref name="module"/>'s default language.
    /// </summary>
    /// <param name="module">The <see cref="ExcelModule"/> to access sheet data from.</param>
    /// <exception cref="InvalidOperationException"><see cref="T"/> does not have a valid <see cref="SheetAttribute"/></exception>
    /// <exception cref="ArgumentException"><see cref="SheetAttribute"/> parameters were invalid (hash mismatch or invalid sheet name)</exception>
    public ExcelSheet( ExcelModule module ) : this( module, module.Language )
    { }

    /// <summary>
    /// Create an <see cref="ExcelSheet{T}"/> instance with a specific <see cref="Data.Language"/>.
    /// </summary>
    /// <param name="module">The <see cref="ExcelModule"/> to access sheet data from.</param>
    /// <param name="requestedLanguage">The language to use for this sheet.</param>
    /// <exception cref="InvalidOperationException"><see cref="T"/> does not have a valid <see cref="SheetAttribute"/></exception>
    /// <exception cref="ArgumentException"><see cref="SheetAttribute"/> parameters were invalid (hash mismatch or invalid sheet name)</exception>
    public ExcelSheet( ExcelModule module, Language requestedLanguage ) : this( module, requestedLanguage, Attribute.Name, Attribute.ColumnHash )
    { }

    /// <summary>
    /// Create an <see cref="ExcelSheet{T}"/> instance with a specific <see cref="Data.Language"/>, name, and hash.
    /// </summary>
    /// <param name="module">The <see cref="ExcelModule"/> to access sheet data from.</param>
    /// <param name="requestedLanguage">The language to use for this sheet.</param>
    /// <param name="sheetName">The name of the sheet to read from.</param>
    /// <param name="columnHash">The hash of the columns in the sheet. If <see langword="null"/>, it will not check the hash.</param>
    /// <exception cref="ArgumentException"><paramref name="sheetName"/> or <paramref name="columnHash"/> parameters were invalid (hash mismatch or invalid sheet name)</exception>
    public ExcelSheet( ExcelModule module, Language requestedLanguage, string sheetName, uint? columnHash = null )
    {
        Module = module;

        var headerFile = module.GameData.GetFile< ExcelHeaderFile >( $"exd/{sheetName}.exh" ) ??
            throw new ArgumentException( "Invalid sheet name", nameof( sheetName ) );

        if( columnHash is { } hash && headerFile.GetColumnsHash() != hash )
            throw new ArgumentException( "Column hash mismatch", nameof( columnHash ) );

        HasSubrows = headerFile.Header.Variant == ExcelVariant.Subrows;

        Language = headerFile.Languages.Contains( requestedLanguage ) ? requestedLanguage : Language.None;

        var rowIds = new List< uint >( (int) headerFile.Header.RowCount );
        var lookups = new List< RowLookup >( (int) headerFile.Header.RowCount );

        if( HasSubrows )
            SubrowDataOffset = headerFile.Header.DataOffset;

        Pages = new( headerFile.DataPages.Length );
        var pageIdx = 0;
        foreach( var pageDef in headerFile.DataPages )
        {
            var filePath = Language == Language.None
                ? $"exd/{sheetName}_{pageDef.StartId}.exd"
                : $"exd/{sheetName}_{pageDef.StartId}_{LanguageUtil.GetLanguageStr( Language )}.exd";
            var fileData = module.GameData.GetFile< ExcelDataFile >( filePath );
            if( fileData == null )
                continue;

            var newPage = new ExcelPage( Module, fileData.Data, headerFile.Header.DataOffset );
            Pages.Add( newPage );

            foreach( var rowPtr in fileData.RowData.Values )
            {
                // var rowDataSize = newPage.ReadUInt32( rowPtr.Offset );
                var subrowCount = newPage.ReadUInt16( rowPtr.Offset + 4 );
                var rowOffset = rowPtr.Offset + 6;

                if( HasSubrows )
                {
                    if( subrowCount > 0 )
                    {
                        rowIds.Add( rowPtr.RowId );
                        lookups.Add( new( pageIdx, rowOffset, subrowCount ) );
                    }
                }
                else
                {
                    rowIds.Add( rowPtr.RowId );
                    lookups.Add( new( pageIdx, rowOffset, 1 ) );
                }
            }

            pageIdx++;
        }

        rowIds.Reverse();
        lookups.Reverse();
        CollectionsMarshal.AsSpan( rowIds ).Sort(CollectionsMarshal.AsSpan( lookups ));
        Keys = [..rowIds];
        Lookup = [..lookups];
        Values = new( this );
    }

    /// <summary>Finds the index of the given row ID.</summary>
    /// <param name="rowId">Row ID to search.</param>
    /// <returns>Zero-based index of the row, or <c>-1</c> if not found.</returns>
    public int IndexOfRow( uint rowId ) => Math.Max( -1, Keys.BinarySearch( rowId ) );

    /// <inheritdoc/>
    public bool HasRow( uint rowId ) => IndexOfRow( rowId ) >= 0;

    /// <inheritdoc/>
    public bool HasSubrow( uint rowId, ushort subrowId )
    {
        if( !HasSubrows )
            throw new NotSupportedException( "Cannot access subrow in a sheet that doesn't support any." );

        var index = IndexOfRow( rowId );
        return index >= 0 && subrowId < Lookup[ index ].SubrowCount;
    }

    /// <inheritdoc/>
    public ushort? TryGetSubrowCount( uint rowId )
    {
        if( !HasSubrows )
            throw new NotSupportedException( "Cannot access subrow in a sheet that doesn't support any." );

        var index = IndexOfRow( rowId );
        return index >= 0 ? Lookup[ index ].SubrowCount : null;
    }

    /// <inheritdoc/>
    public ushort GetSubrowCount( uint rowId )
    {
        if( !HasSubrows )
            throw new NotSupportedException( "Cannot access subrow in a sheet that doesn't support any." );

        var index = IndexOfRow( rowId );
        return index >= 0 ? Lookup[ index ].SubrowCount : throw new ArgumentOutOfRangeException( nameof( rowId ), "Row does not exist" );
    }

    private T CreateRow( uint rowId, in RowLookup val ) =>
        T.Create( Pages[ val.PageIdx ], val.Offset, rowId );

    private T CreateRowByIndex( int rowIndex ) => CreateRow( Keys[ rowIndex ], Lookup[ rowIndex ] );

    private T CreateSubrow( uint rowId, ushort subrowId, in RowLookup val ) =>
        T.Create( Pages[ val.PageIdx ], val.Offset + 2 + ( subrowId * ( SubrowDataOffset + 2u ) ), rowId, subrowId );

    private T CreateSubrowByIndex( int rowIndex, ushort subrowId ) => CreateSubrow( Keys[ rowIndex ], subrowId, Lookup[ rowIndex ] );

    /// <summary>
    /// Tries to get the <paramref name="rowId"/>th row in this sheet. If this sheet has subrows, it will return the first subrow.
    /// </summary>
    /// <param name="rowId">The row id to get</param>
    /// <returns>A nullable row object. Returns null if the row does not exist.</returns>
    public T? TryGetRow( uint rowId )
    {
        if( HasSubrows )
            return TryGetSubrow( rowId, 0 );

        var index = IndexOfRow( rowId );
        return index < 0 ? null : CreateRowByIndex( index );
    }

    /// <summary>
    /// Tries to get the <paramref name="subrowId"/>th subrow from the <paramref name="rowId"/>th row in this sheet.
    /// </summary>
    /// <param name="rowId">The row id to get</param>
    /// <param name="subrowId">The subrow id to get</param>
    /// <returns>A nullable row object. Returns null if the subrow does not exist.</returns>
    /// <exception cref="NotSupportedException">Thrown if the sheet does not support subrows.</exception>
    public T? TryGetSubrow( uint rowId, ushort subrowId )
    {
        if( !HasSubrows )
            throw new NotSupportedException( "Cannot access subrow in a sheet that doesn't support any." );

        var index = IndexOfRow( rowId );
        if( index < 0 )
            return null;
        if( subrowId >= Lookup[ index ].SubrowCount )
            return null;
        return CreateSubrowByIndex( index, subrowId );
    }

    /// <summary>
    /// Gets the <paramref name="rowId"/>th row in this sheet. If this sheet has subrows, it will return the first subrow. Throws if the row does not exist.
    /// </summary>
    /// <param name="rowId">The row id to get</param>
    /// <returns>A row object.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the sheet does not have a row at that <paramref name="rowId"/></exception>
    public T GetRow( uint rowId ) =>
        TryGetRow( rowId ) ??
        throw new ArgumentOutOfRangeException( nameof( rowId ), "Row does not exist" );

    /// <summary>
    /// Gets the <paramref name="subrowId"/>th subrow from the <paramref name="rowId"/>th row in this sheet. Throws if the subrow does not exist.
    /// </summary>
    /// <param name="rowId">The row id to get</param>
    /// <param name="subrowId">The subrow id to get</param>
    /// <returns>A row object.</returns>
    /// <exception cref="NotSupportedException">Thrown if the sheet does not support subrows.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the sheet does not have a row at that <paramref name="rowId"/></exception>
    public T GetSubrow( uint rowId, ushort subrowId )
    {
        if( !HasSubrows )
            throw new NotSupportedException( "Cannot access subrow in a sheet that doesn't support any." );

        var index = IndexOfRow( rowId );
        if( index < 0 )
            throw new ArgumentOutOfRangeException( nameof( rowId ), "Row does not exist" );
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual( subrowId, Lookup[ index ].SubrowCount );

        return CreateSubrowByIndex( index, subrowId );
    }

    /// <summary>
    /// Returns an enumerator that can be used to iterate over all subrows in all rows in this sheet.
    /// </summary>
    /// <returns>An <see cref="IEnumerator{T}"/> of all subrows in this sheet</returns>
    /// <exception cref="NotSupportedException">Thrown if the sheet does not support Rows</exception>
    public RowList.RowEnumerator GetSubrowEnumerator()
    {
        if( !HasSubrows )
            throw new NotSupportedException( "Cannot enumerate Rows in a sheet that doesn't support any." );

        return new( this, ushort.MaxValue );
    }

    /// <summary>Row lookup data.</summary>
    /// <param name="PageIdx">Index of the page.</param>
    /// <param name="Offset">Byte offset of the row in the page.</param>
    /// <param name="SubrowCount">Number of subrows. <c>1</c> if <see cref="ExcelSheet{T}.HasSubrows"/> is <see langword="false"/>.</param>
    private record struct RowLookup( int PageIdx, uint Offset, ushort SubrowCount );
}