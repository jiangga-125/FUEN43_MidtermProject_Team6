using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models.Dto
{
    public class PenatlyRulesDto
    {
        public int RuleID { get; set; }
       
        public string ReasonCode { get; set; }
        
        public string ChargeType { get; set; }

        public int UnitAmount { get; set; }

    }
}
