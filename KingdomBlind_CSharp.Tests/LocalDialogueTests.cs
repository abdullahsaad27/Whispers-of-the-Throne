using Xunit;
using KingdomBlind_CSharp.Data;

namespace KingdomBlind_CSharp.Tests
{
    public class LocalDialogueTests
    {
        private readonly LocalDialogueService _service;

        public LocalDialogueTests()
        {
            _service = new LocalDialogueService();
        }

        [Fact]
        public void GetLine_RoleMatch_ReturnsCorrectLine()
        {
            string line = _service.GetLine("قائد", "حرب", "المنصور");
            Assert.Contains("إن أردت الحرب فلتسبقها المؤونة", line);
            Assert.Contains("المنصور", line);
        }

        [Fact]
        public void GetLine_ContextMatch_ReturnsDefaultContextLine()
        {
            string line = _service.GetLine("طباخ", "tax", "الرشيد");
            Assert.Contains("المال الهادئ أطول عمراً", line);
            Assert.Contains("الرشيد", line);
        }

        [Fact]
        public void GetLine_NoMatch_ReturnsDefaultLine()
        {
            string line = _service.GetLine("فلاح", "زراعة", "المأمون");
            Assert.Contains("القرار الحكيم يبدأ بسؤال: من سيكسب من هذا", line);
            Assert.Contains("المأمون", line);
        }
    }
}
