// ReSharper disable All

using Lumina.Text;
using Lumina.Data;
using Lumina.Data.Structs.Excel;

namespace Lumina.Excel.GeneratedSheets
{
    [Sheet( "LeveRewardItemGroup", columnHash: 0xf065e622 )]
    public class LeveRewardItemGroup : ExcelRow
    {
        public class UnkData0Obj
        {
            public int Item;
            public byte Count;
            public bool HQ;
        }
        
        public UnkData0Obj[] UnkData0 { get; set; }
        
        public override void PopulateData( RowParser parser, GameData gameData, Language language )
        {
            base.PopulateData( parser, gameData, language );

            UnkData0 = new UnkData0Obj[ 9 ];
            for( var i = 0; i < 9; i++ )
            {
                UnkData0[ i ] = new UnkData0Obj();
                UnkData0[ i ].Item = parser.ReadColumn< int >( 0 + ( i * 3 + 0 ) );
                UnkData0[ i ].Count = parser.ReadColumn< byte >( 0 + ( i * 3 + 1 ) );
                UnkData0[ i ].HQ = parser.ReadColumn< bool >( 0 + ( i * 3 + 2 ) );
            }
        }
    }
}