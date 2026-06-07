using System;

namespace KingdomBlind_CSharp.Models
{
    public class Character
    {
        public string Role { get; set; }
        private Random rand = new Random();

        public Character(string role)
        {
            Role = role;
        }

        public string GetDynamicResponse(GameState state)
        {
            if (Role == "Vizier")
            {
                if (state.Gold < 200)
                {
                    string[] lowGold = {
                        "سيدي، خزائننا تكاد تفرغ، يجب أن نجمع الضرائب بأسرع وقت.",
                        "الوضع المالي حرج يا مولاي، لا يمكننا الاستمرار دون ذهب.",
                        "الفقر يطرق أبواب القلعة، علينا زيادة الدخل فوراً."
                    };
                    return lowGold[rand.Next(lowGold.Length)];
                }
                else if (state.Satisfaction < 50)
                {
                    string[] lowHappy = {
                        "الرعية في حالة غليان يا سيدي، احذر من التمرد.",
                        "الناس يتذمرون في الأسواق، السعادة منخفضة جداً.",
                        "علينا تخفيف الضرائب أو إقامة احتفال، الشعب غاضب."
                    };
                    return lowHappy[rand.Next(lowHappy.Length)];
                }
                else
                {
                    string[] normal = {
                        "المملكة في حالة استقرار يا مولاي، كل شيء يسير حسب الخطة.",
                        "خزائننا عامرة وشعبنا راضٍ، إنها فترة ذهبية.",
                        "الأمور هادئة، هل نركز على تعزيز الجيش الآن؟"
                    };
                    return normal[rand.Next(normal.Length)];
                }
            }
            else if (Role == "Queen")
            {
                if (state.Army < 50)
                {
                    string[] weakArmy = {
                        "زوجي العزيز، أشعر بالقلق، حرس القلعة قليل جداً.",
                        "أرجوك، عزز دفاعاتنا، الأعداء يتربصون بنا وجيشنا ضعيف.",
                        "لا أشعر بالأمان، نحتاج لمزيد من الجنود لحمايتنا."
                    };
                    return weakArmy[rand.Next(weakArmy.Length)];
                }
                else if (state.Satisfaction > 80 && state.Gold > 500)
                {
                    string[] prosperous = {
                        "الناس في الشوارع يهتفون باسمك، أنت أعظم ملك في تاريخنا.",
                        "المملكة تزدهر في عهدك، أنا فخورة جداً بك.",
                        "الأمان يعم الأرجاء، هل نُقيم وليمة كبرى احتفالاً بذلك؟"
                    };
                    return prosperous[rand.Next(prosperous.Length)];
                }
                else
                {
                    string[] normal = {
                        "القصر هادئ اليوم، كيف أستطيع مساعدتك يا مليكي؟",
                        "وجودك بجانبي يشعرني بالطمأنينة.",
                        "أتمنى أن تكون قراراتك اليوم موفقة."
                    };
                    return normal[rand.Next(normal.Length)];
                }
            }
            return "مرحباً يا سيدي.";
        }
    }
}
