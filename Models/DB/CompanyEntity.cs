using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace growy_server.Models.DB
{
    [Table("companies")]
    public class CompanyEntity
    {
        [Key]
        [Column("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [Column("company_name")]
        public string? CompanyName { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("sector")]
        public string? Sector { get; set; }

        [Column("industry")]
        public string? Industry { get; set; }

        [Column("market_capitalization")]
        public decimal? MarketCapitalization { get; set; }

        [Column("eps")]
        public decimal? Eps { get; set; }

        [Column("analyst_target_price")]
        public decimal? AnalystTargetPrice { get; set; }

        [Column("analyst_rating_strong_buy")]
        public int? AnalystRatingStrongBuy { get; set; }

        [Column("analyst_rating_buy")]
        public int? AnalystRatingBuy { get; set; }

        [Column("analyst_rating_hold")]
        public int? AnalystRatingHold { get; set; }

        [Column("analyst_rating_sell")]
        public int? AnalystRatingSell { get; set; }

        [Column("analyst_rating_strong_sell")]
        public int? AnalystRatingStrongSell { get; set; }

        [Column("week_52_high")]
        public decimal? Week52High { get; set; }

        [Column("week_52_low")]
        public decimal? Week52Low { get; set; }

        [Column("moving_avg_50_day")]
        public decimal? MovingAvg50Day { get; set; }

        [Column("moving_avg_200_day")]
        public decimal? MovingAvg200Day { get; set; }

        [Column("exchange")]
        public string? Exchange { get; set; }

        [Column("has_cedear")]
        public bool HasCedear { get; set; }

        [Column("is_toxic")]
        public bool IsToxic { get; set; }

        [Column("is_top_growth")]
        public bool IsTopGrowth { get; set; }
    }
}
