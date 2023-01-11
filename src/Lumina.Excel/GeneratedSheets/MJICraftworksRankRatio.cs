// ReSharper disable All

using Lumina.Text;
using Lumina.Data;
using Lumina.Data.Structs.Excel;

namespace Lumina.Excel.GeneratedSheets
{
    [Sheet( "MJICraftworksRankRatio", columnHash: 0xd870e208 )]
    public partial class MJICraftworksRankRatio : ExcelRow
    {
        
        public ushort Ratio { get; set; }
        
        public override void PopulateData( RowParser parser, GameData gameData, Language language )
        {
            base.PopulateData( parser, gameData, language );

            Ratio = parser.ReadColumn< ushort >( 0 );
        }
    }
}