using System;
using Lumina.Data;
using Lumina.Excel.Sheets;

namespace Lumina.Excel.Rows;

/// <summary>
/// A helper type to dynamically reference a row in a specific Excel sheet.
/// </summary>
/// <param name="module">The <see cref="ExcelModule"/> to read sheet data from.</param>
/// <param name="rowId">The referenced row id.</param>
/// <param name="rowType">The referenced row's actual <see cref="Type"/>.</param>
public readonly struct RowRef( ExcelModule? module, uint rowId, Type? rowType )
{
    /// <summary>
    /// The row id of the referenced row.
    /// </summary>
    public uint RowId => rowId;

    /// <summary>
    /// Whether the <see cref="RowRef"/> is untyped.
    /// </summary>
    /// <remarks>
    /// An untyped <see cref="RowRef"/> is one that doesn't know which sheet it links to.
    /// </remarks>
    public bool IsUntyped => rowType == null;

    /// <summary>
    /// Whether the reference is of a specific row type.
    /// </summary>
    /// <typeparam name="T">The row type/schema to check against.</typeparam>
    /// <returns>Whether this <see cref="RowRef"/> points to a <typeparamref name="T"/>.</returns>
    public bool Is< T >() where T : struct, IExcelRow< T > =>
        typeof( T ) == rowType;

    /// <inheritdoc cref="Is{T}"/>
    public bool IsSubrow< T >() where T : struct, IExcelRow< T > =>
        typeof( T ) == rowType;

    /// <summary>
    /// Tries to get the referenced row as a specific row type.
    /// </summary>
    /// <typeparam name="T">The row type/schema to check against.</typeparam>
    /// <returns>The referenced row type. Returns null if this <see cref="RowRef"/> does not point to a <typeparamref name="T"/> or if the <see cref="RowId"/> does not exist in its sheet.</returns>
    public T? GetValueOrDefault< T >() where T : struct, IExcelRow< T >
    {
        if( !Is< T >() || module is null )
            return null;

        return new RowRef< T >( module, rowId ).ValueNullable;
    }

    /// <inheritdoc cref="GetValueOrDefault{T}"/>
    public SubrowExcelSheet< T >.SubrowCollection? GetValueOrDefaultSubrow< T >() where T : struct, IExcelRow< T >
    {
        if( !IsSubrow< T >() || module is null )
            return null;

        return new SubrowRef< T >( module, rowId ).ValueNullable;
    }

    /// <summary>
    /// Tries to get the referenced row as a specific row type.
    /// </summary>
    /// <typeparam name="T">The row type/schema to check against.</typeparam>
    /// <param name="row">The output row object.</param>
    /// <returns><see langword="true"/> if the type is valid, the row exists, and <paramref name="row"/> is written to, and <see langword="false"/> otherwise.</returns>
    public bool TryGetValue< T >( out T row ) where T : struct, IExcelRow< T >
    {
        if( new RowRef< T >( module, rowId ).ValueNullable is { } v )
        {
            row = v;
            return true;
        }

        row = default;
        return false;
    }

    /// <inheritdoc cref="TryGetValue{T}(out T)"/>
    public bool TryGetValueSubrow< T >( out SubrowExcelSheet< T >.SubrowCollection row ) where T : struct, IExcelRow< T >
    {
        if( new SubrowRef< T >( module, rowId ).ValueNullable is { } v )
        {
            row = v;
            return true;
        }

        row = default;
        return false;
    }

    /// <summary>
    /// Attempts to create a <see cref="RowRef"/> to a row id of a list of row types, checking with each type in order.
    /// </summary>
    /// <param name="module">The <see cref="ExcelModule"/> to read sheet data from.</param>
    /// <param name="language">Desired language if any, or <see langword="null"/> to use the langauge specified from <paramref name="module"/>.</param>
    /// <param name="rowId">The referenced row id.</param>
    /// <param name="sheetTypes">A list of row types to check against the <paramref name="rowId"/>, in order.</param>
    /// <returns>A <see cref="RowRef"/> to one of the <paramref name="sheetTypes"/>. If the row id does not exist in any of the sheets, an untyped <see cref="RowRef"/> is returned instead.</returns>
    public static RowRef GetFirstValidRowOrUntyped( ExcelModule module, Language? language, uint rowId, params Type[] sheetTypes )
    {
        foreach( var sheetType in sheetTypes )
        {
            if( module.GetSheetAttributes( sheetType ) is not { Name: not null } sa )
                continue;

            try
            {
                var rawSheet = module.GetRawSheet( sa.Name, language ?? module.GameData.Options.DefaultExcelLanguage );
                if( rawSheet.HasRow( rowId ) )
                    return new( module, rowId, sheetType );
            }
            catch
            {
                // ignore and try next
            }
        }

        return CreateUntyped( rowId );
    }

    /// <summary>
    /// Creates a <see cref="RowRef"/> to a specific row type.
    /// </summary>
    /// <typeparam name="T">The row type referenced by the <paramref name="rowId"/>.</typeparam>
    /// <param name="module">The <see cref="ExcelModule"/> to read sheet data from.</param>
    /// <param name="rowId">The referenced row id.</param>
    /// <returns>A <see cref="RowRef"/> to a row in a <see cref="RawExcelSheet"/>.</returns>
    public static RowRef Create< T >( ExcelModule? module, uint rowId ) where T : struct, IExcelRow< T > => new( module, rowId, typeof( T ) );

    /// <inheritdoc cref="Create{T}(ExcelModule?, uint)"/>
    public static RowRef CreateSubrow< T >( ExcelModule? module, uint rowId ) where T : struct, IExcelRow< T > => new( module, rowId, typeof( T ) );

    /// <summary>
    /// Creates an untyped <see cref="RowRef"/>.
    /// </summary>
    /// <param name="rowId">The referenced row id.</param>
    /// <returns>An untyped <see cref="RowRef"/>.</returns>
    public static RowRef CreateUntyped( uint rowId ) => new( null, rowId, null );
}