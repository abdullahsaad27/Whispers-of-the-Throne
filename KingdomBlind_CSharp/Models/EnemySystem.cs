using System;

namespace KingdomBlind_CSharp.Models
{
    public class EnemySystem
    {
        public int EnemyArmy { get; set; } = 150;
        public int EnemyGold { get; set; } = 500;
        public string KingdomName { get; set; } = "المملكة المجاورة";
        
        private Random rand = new Random();

        public string ProcessTurn(GameState playerState)
        {
            EnemyGold += 100; // Income
            
            if (playerState.Army > EnemyArmy + 50)
            {
                if (EnemyGold >= 150)
                {
                    EnemyGold -= 150;
                    EnemyArmy += 100;
                    string[] panic = {
                        "الجواسيس يخبروننا أن العدو يعزز دفاعاته بشكل جنوني خوفاً من جيشك القوي!",
                        "المملكة المجاورة أعلنت حالة الطوارئ وتقوم بتجنيد كل من يستطيع حمل السلاح لمواجهتك.",
                        "حراس الحدود لاحظوا تحركات كثيفة، العدو يحشد قواته للدفاع عن نفسه."
                    };
                    return panic[rand.Next(panic.Length)];
                }
            }
            else if (EnemyArmy > playerState.Army * 2)
            {
                string[] aggressive = {
                    "ملك الأعداء يرسل لك رسالة استهزاء بضعف قواتك!",
                    "قوات العدو تتجمع على الحدود وتستفز حرسنا، جيشهم يفوقنا عدداً بمراحل.",
                    "العدو يرسل تهديداً مباشراً: استسلموا أو ستسحقكم جيوشنا الجرارة قريبًا."
                };
                return aggressive[rand.Next(aggressive.Length)];
            }
            else
            {
                if (EnemyGold >= 100)
                {
                    EnemyGold -= 100;
                    EnemyArmy += 50;
                    string[] normal = {
                        "العدو يواصل تدريب قواته في الخفاء، استعدوا.",
                        "حركة تجارية وعسكرية طبيعية في المملكة المجاورة، لا شيء مريب.",
                        "لا توجد تحركات عدائية واضحة، لكنهم يبنون جيشهم ببطء كالمعتاد."
                    };
                    return normal[rand.Next(normal.Length)];
                }
            }
            
            return "المملكة المجاورة هادئة هذا الدور ولم تقم بأي تحرك.";
        }
    }
}
