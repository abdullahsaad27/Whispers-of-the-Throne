using System;
using System.Collections.Generic;

namespace KingdomBlind_CSharp.Models
{
    public class TimeState
    {
        public int Day { get; set; } = 1;
        public int Month { get; set; } = 1;
        public int Year { get; set; } = 1000;
        public bool IsPaused { get; set; } = true;
        
        public string GetDateString()
        {
            string[] months = { "البذار", "المطر", "الزهور", "الحصاد", "القيظ", "الرياح", "الصيد", "السيوف", "الخريف", "الضباب", "الثلج", "الملوك" };
            string mName = (Month >= 1 && Month <= 12) ? months[Month - 1] : $"شهر {Month}";
            return $"اليوم {Day} من شهر {mName}، عام {Year}";
        }

        public void AddDays(int days)
        {
            Day += days;
            while (Day > 30)
            {
                Day -= 30;
                Month++;
                if (Month > 12)
                {
                    Month = 1;
                    Year++;
                }
            }
        }
    }
}
