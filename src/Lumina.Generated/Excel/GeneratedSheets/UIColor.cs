using Lumina.Data.Structs.Excel;

namespace Lumina.Excel.GeneratedSheets
{
    [Sheet( "UIColor", columnHash: 0x96a22aea )]
    public class UIColor : IExcelRow
    {
        
        public uint UIForeground;
        public uint UIGlow;
        public uint Unknown2;
        
        public uint RowId { get; set; }
        public uint SubRowId { get; set; }

        public void PopulateData( RowParser parser, Lumina lumina )
        {
            RowId = parser.Row;
            SubRowId = parser.SubRow;

            UIForeground = parser.ReadColumn< uint >( 0 );
            UIGlow = parser.ReadColumn< uint >( 1 );
            Unknown2 = parser.ReadColumn< uint >( 2 );
        }
    }
}