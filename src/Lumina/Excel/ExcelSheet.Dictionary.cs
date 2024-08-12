using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Lumina.Excel;

public sealed partial class ExcelSheet< T >
{
    /// <summary>Gets the key collection, sorted by row IDs.</summary>
    public ImmutableList< uint > Keys { get; }

    /// <summary>Gets the value collection, sorted by row IDs.</summary>
    public RowList Values { get; }

    /// <inheritdoc/>
    bool ICollection< KeyValuePair< uint, T > >.IsReadOnly => true;

    /// <inheritdoc/>
    bool IDictionary.IsFixedSize => true;

    /// <inheritdoc/>
    bool IDictionary.IsReadOnly => true;

    /// <inheritdoc/>
    bool ICollection.IsSynchronized => false;

    /// <inheritdoc/>
    object ICollection.SyncRoot => typeof( ExcelSheet< T > );

    /// <inheritdoc/>
    ICollection IDictionary.Keys => Keys;

    /// <inheritdoc/>
    ICollection IDictionary.Values => Values;

    /// <inheritdoc/>
    IEnumerable< uint > IReadOnlyDictionary< uint, T >.Keys => Keys;

    /// <inheritdoc/>
    IEnumerable< T > IReadOnlyDictionary< uint, T >.Values => Values;

    /// <inheritdoc/>
    ICollection< uint > IDictionary< uint, T >.Keys => Keys;

    /// <inheritdoc/>
    ICollection< T > IDictionary< uint, T >.Values => Values;

    /// <summary>Gets the row with the given row ID.</summary>
    /// <param name="rowId">Row ID.</param>
    public T this[ uint rowId ] => TryGetRow( rowId ) ?? throw new ArgumentOutOfRangeException(nameof(rowId));

    /// <inheritdoc/>
    T IDictionary< uint, T >.this[ uint key ] {
        get => this[ key ];
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    object? IDictionary.this[ object key ] {
        get => key is uint u ? this[ u ] : throw new KeyNotFoundException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    bool IDictionary.Contains( object key ) => key is uint u && ContainsKey( u );

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    public Enumerator GetEnumerator() => new( this );

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    IEnumerator< KeyValuePair< uint, T > > IEnumerable< KeyValuePair< uint, T > >.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    IDictionaryEnumerator IDictionary.GetEnumerator() => GetEnumerator();

    /// <inheritdoc cref="IEnumerable.GetEnumerator"/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc cref="ICollection{T}.Contains"/>
    public bool Contains( KeyValuePair< uint, T > item ) => TryGetRow( item.Key ) is { } value && EqualityComparer< T >.Default.Equals( value, item.Value );

    /// <inheritdoc cref="ICollection{T}.CopyTo"/>
    public void CopyTo( KeyValuePair< uint, T >[] array, int arrayIndex )
    {
        ArgumentNullException.ThrowIfNull( array );
        ArgumentOutOfRangeException.ThrowIfNegative( arrayIndex );
        if( Count > array.Length - arrayIndex )
            throw new ArgumentException( "The number of elements in the source list is greater than the available space." );

        if( HasSubrows )
        {
            for( var i = 0; i < Lookup.Length; i++ )
                array[ arrayIndex++ ] = new( Keys[ i ], CreateSubrowByIndex( i, 0 ) );
        }
        else
        {
            for( var i = 0; i < Lookup.Length; i++ )
                array[ arrayIndex++ ] = new( Keys[ i ], CreateRowByIndex( i ) );
        }
    }

    /// <inheritdoc/>
    void ICollection.CopyTo( Array array, int index )
    {
        ArgumentNullException.ThrowIfNull( array );
        ArgumentOutOfRangeException.ThrowIfNegative( index );
        if( Count > array.Length - index )
            throw new ArgumentException( "The number of elements in the source list is greater than the available space." );

        if( HasSubrows )
        {
            for( var i = 0; i < Lookup.Length; i++ )
                array.SetValue( new DictionaryEntry( Keys[ i ], CreateSubrowByIndex( i, 0 ) ), index++ );
        }
        else
        {
            for( var i = 0; i < Lookup.Length; i++ )
                array.SetValue( new DictionaryEntry( Keys[ i ], CreateRowByIndex( i ) ), index++ );
        }
    }

    /// <inheritdoc cref="IDictionary{TKey,TValue}.ContainsKey"/>
    public bool ContainsKey( uint key ) => Keys.BinarySearch( key ) != -1;

    /// <inheritdoc cref="IDictionary{TKey,TValue}.TryGetValue"/>
    public bool TryGetValue( uint key, out T value )
    {
        if( TryGetRow( key ) is { } v )
        {
            value = v;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    void ICollection< KeyValuePair< uint, T > >.Add( KeyValuePair< uint, T > item ) => throw new NotSupportedException();

    /// <inheritdoc/>
    void ICollection< KeyValuePair< uint, T > >.Clear() => throw new NotSupportedException();

    /// <inheritdoc/>
    bool ICollection< KeyValuePair< uint, T > >.Remove( KeyValuePair< uint, T > item ) => throw new NotSupportedException();

    /// <inheritdoc/>
    void IDictionary.Add( object key, object? value ) => throw new NotSupportedException();

    /// <inheritdoc/>
    void IDictionary.Clear() => throw new NotSupportedException();

    /// <inheritdoc/>
    void IDictionary.Remove( object key ) => throw new NotSupportedException();

    /// <inheritdoc/>
    void IDictionary< uint, T >.Add( uint key, T value ) => throw new NotSupportedException();

    /// <inheritdoc/>
    bool IDictionary< uint, T >.Remove( uint key ) => throw new NotSupportedException();

    /// <summary>Enumerator for <see cref="ExcelSheet{T}"/>.</summary>
    /// <param name="sheet">Sheet to enumerate.</param>
    public struct Enumerator( ExcelSheet< T > sheet ) : IDictionaryEnumerator, IEnumerator< KeyValuePair< uint, T > >
    {
        private int _index = -1;

        /// <inheritdoc cref="IEnumerator{T}.Current"/>
        public KeyValuePair< uint, T > Current { get; private set; }

        /// <inheritdoc/>
        object IEnumerator.Current => Current;

        /// <inheritdoc/>
        DictionaryEntry IDictionaryEnumerator.Entry => new( Current.Key, Current.Value );

        /// <inheritdoc/>
        object IDictionaryEnumerator.Key => Current.Key;

        /// <inheritdoc/>
        object IDictionaryEnumerator.Value => Current.Value;

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if( _index + 1 >= sheet.Count )
                return false;

            _index++;
            Current = new( sheet.Keys[ _index ], sheet.Values[ _index ] );
            return true;
        }

        /// <inheritdoc/>
        public void Reset() => _index = -1;

        /// <inheritdoc/>
        public void Dispose()
        { }
    }

    /// <summary>List of rows.</summary>
    /// <param name="sheet">Owner sheet.</param>
    public sealed class RowList( ExcelSheet< T > sheet ) : IList< T >, IReadOnlyList< T >, ICollection
    {
        /// <inheritdoc cref="ICollection{T}.Count"/>
        public int Count => sheet.Count;

        /// <inheritdoc/>
        bool ICollection.IsSynchronized => false;

        /// <inheritdoc/>
        object ICollection.SyncRoot => typeof( RowList );

        /// <inheritdoc cref="ICollection{T}.IsReadOnly"/>
        bool ICollection< T >.IsReadOnly => true;

        /// <inheritdoc cref="IReadOnlyList{T}.this"/>
        public T this[ int index ] {
            get {
                ArgumentOutOfRangeException.ThrowIfNegative( index );
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual( index, sheet.Count );
                return sheet.HasSubrows ? sheet.CreateSubrowByIndex( index, 0 ) : sheet.CreateRowByIndex( index );
            }
        }

        /// <inheritdoc cref="IList{T}.this"/>
        T IList< T >.this[ int index ] {
            get => this[ index ];
            set => throw new NotSupportedException();
        }

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
        RowEnumerator GetEnumerator() => new( sheet, 0 );

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator< T > IEnumerable< T >.GetEnumerator() => GetEnumerator();

        /// <inheritdoc cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc cref="ICollection{T}.Contains"/>
        public bool Contains( T item ) => this.Any( v => EqualityComparer< T >.Default.Equals( v, item ) );

        /// <inheritdoc cref="ICollection{T}.CopyTo"/>
        public void CopyTo( T[] array, int arrayIndex )
        {
            ArgumentNullException.ThrowIfNull( array );
            ArgumentOutOfRangeException.ThrowIfNegative( arrayIndex );
            if( sheet.Count > array.Length - arrayIndex )
                throw new ArgumentException( "The number of elements in the source list is greater than the available space." );

            foreach( var row in this )
                array[ arrayIndex++ ] = row;
        }

        /// <inheritdoc/>
        void ICollection.CopyTo( Array array, int index )
        {
            ArgumentNullException.ThrowIfNull( array );
            ArgumentOutOfRangeException.ThrowIfNegative( index );
            if( sheet.Count > array.Length - index )
                throw new ArgumentException( "The number of elements in the source list is greater than the available space." );

            foreach( var row in this )
                array.SetValue( row, index++ );
        }

        /// <inheritdoc cref="IList{T}.IndexOf"/>
        public int IndexOf( T item )
        {
            var count = Count;
            for (var index = 0; index < count; index++)
            {
                if( EqualityComparer< T >.Default.Equals( this[ index ], item ) )
                    return index;
            }

            return -1;
        }

        /// <inheritdoc/>
        void ICollection< T >.Add( T item ) => throw new NotSupportedException();

        /// <inheritdoc/>
        void ICollection< T >.Clear() => throw new NotSupportedException();

        /// <inheritdoc/>
        bool ICollection< T >.Remove( T item ) => throw new NotSupportedException();

        /// <inheritdoc/>
        void IList< T >.Insert( int index, T item ) => throw new NotSupportedException();

        /// <inheritdoc/>
        void IList< T >.RemoveAt( int index ) => throw new NotSupportedException();

        /// <summary>Enumerates over rows in a sheet.</summary>
        /// <param name="sheet">Sheet to enumerate its rows.</param>
        public struct RowEnumerator( ExcelSheet< T > sheet, ushort maxSubrowId ) : IEnumerator< T >, IEnumerable< T >
        {
            private int _index = -1;
            private ushort _subrowIndex = 0;

            /// <inheritdoc cref="IEnumerator{T}.Current"/>
            public T Current { get; private set; }

            /// <inheritdoc/>
            object IEnumerator.Current => Current;

            /// <inheritdoc/>
            public bool MoveNext()
            {
                if( sheet.HasSubrows )
                {
                    if( _index == -1 )
                    {
                        if( sheet.Count == 0 )
                            return false;

                        _index = 0;
                        _subrowIndex = 0;
                    }
                    else if( _subrowIndex + 1 >= sheet.Lookup[ _index ].SubrowCount || _subrowIndex + 1 > maxSubrowId )
                    {
                        if( _index + 1 >= sheet.Count )
                            return false;

                        _subrowIndex = 0;
                        _index++;
                    }
                    else
                    {
                        _subrowIndex++;
                    }
                }
                else
                {
                    if( _index + 1 >= sheet.Count )
                        return false;

                    _index++;
                }

                Current = sheet.HasSubrows
                    ? sheet.CreateSubrowByIndex( _index, _subrowIndex )
                    : sheet.CreateRowByIndex( _index );
                return true;
            }

            /// <inheritdoc/>
            public void Reset() => _index = -1;

            /// <inheritdoc/>
            public void Dispose()
            { }

            /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
            public RowEnumerator GetEnumerator() => new( sheet, maxSubrowId );

            /// <inheritdoc/>
            IEnumerator< T > IEnumerable< T >.GetEnumerator() => GetEnumerator();

            /// <inheritdoc/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}