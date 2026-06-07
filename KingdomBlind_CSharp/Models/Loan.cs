using System;

namespace KingdomBlind_CSharp.Models
{
    public class Loan
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string LenderType { get; set; } // Merchants, ForeignKingdom
        public string LenderName { get; set; }
        
        public int PrincipalAmount { get; set; }
        public int RemainingAmount { get; set; }
        
        public int StartDateDays { get; set; }
        public int DueDateDays { get; set; }
        
        public string RepaymentMode { get; set; } = "Automatic"; // Automatic, Manual
        public int ScheduledPaymentAmount { get; set; } = 10;
        
        public string PoliticalCondition { get; set; } = "";
        
        public bool IsDefaulted { get; set; } = false;
        public int TrustPenalty { get; set; } = 0;
        
        public string Notes { get; set; } = "";
    }
}
