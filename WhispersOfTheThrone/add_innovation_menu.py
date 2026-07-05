import sys
import io

def patch_mainform():
    with io.open('MainForm.cs', 'r', encoding='utf-8') as f:
        content = f.read()

    # Revert the wrong patch
    wrong_code = """            SetNarrativeText($"في أي سن تولى الخليفة {state.RulerName} الحكم؟", false);
            
            AddActionButton("شاب (20 عاماً)", (s, e) => { state.RulerAge = 20; ShowMaritalSelection(); });
            AddActionButton("ناضج (30 عاماً)", (s, e) => { state.RulerAge = 30; ShowMaritalSelection(); });
            AddActionButton("مخضرم (40 عاماً)", (s, e) => { state.RulerAge = 40; ShowMaritalSelection(); });
            AddActionButton("متقدم في السن (50 عاماً)", (s, e) => { state.RulerAge = 50; ShowMaritalSelection(); });"""
            
    correct_code = """            AddActionButton("شاب (20 عاماً) - حيوية ولكن خبرة أقل", (s, e) => { state.RulerAge = 20; ShowMaritalSelection(); });
            AddActionButton("ناضج (30 عاماً) - توازن بين الشباب والخبرة", (s, e) => { state.RulerAge = 30; ShowMaritalSelection(); });
            AddActionButton("مخضرم (40 عاماً) - حكمة في الإدارة", (s, e) => { state.RulerAge = 40; ShowMaritalSelection(); });
            AddActionButton("متقدم في السن (50 عاماً) - هيبة كبرى ولكن صحة أضعف", (s, e) => { state.RulerAge = 50; ShowMaritalSelection(); });"""
            
    if "في أي سن تولى الخليفة" in content:
        content = content.replace(wrong_code, correct_code).replace("في أي سن تولى الخليفة", "في أي مرحلة عمرية تولى الخليفة")

    # Add ShowEraInnovationMenu
    if "private void ShowEraInnovationMenu" not in content:
        method_str = """
        private void ShowEraInnovationMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_era_innovations";
            SetScreenTitle("الابتكارات الثقافية والعصور");
            
            EraInnovationSystem.UpdateCurrentEraBasedOnYear(state);
            string eraName = state.CurrentEra switch {
                HistoricalEra.Tribal => "عصر القبائل",
                HistoricalEra.EarlyMedieval => "العصور الوسطى المبكرة",
                HistoricalEra.HighMedieval => "العصور الوسطى العليا",
                HistoricalEra.LateMedieval => "العصور الوسطى المتأخرة",
                _ => state.CurrentEra.ToString()
            };

            string currentTarget = string.IsNullOrEmpty(state.TargetInnovation) ? "لا يوجد" : state.TargetInnovation;
            
            string info = $"العصر الحالي: {eraName} (السنة {state.Time.Year})\\n" +
                          $"الابتكار المستهدف حالياً: {currentTarget}\\n\\n" +
                          "الابتكارات المتاحة:\\n";
            
            var innovations = state.ActiveCultureInnovations ?? new List<Innovation>();
            if (innovations.Count == 0)
            {
                info += "لا توجد ابتكارات متاحة حالياً.\\n";
            }
            else
            {
                foreach(var inv in innovations)
                {
                    string status = inv.IsUnlocked ? "[مفتوح]" : $"[قيد التقدم: {inv.Progress}/{inv.CostPoints}]";
                    info += $"- {inv.Name} {status}: {inv.Description}\\n";
                }
            }

            SetNarrativeText(info, true);
            
            var lockedInvs = innovations.Where(i => !i.IsUnlocked).ToList();
            foreach (var inv in lockedInvs)
            {
                if (inv.Name != state.TargetInnovation)
                {
                    AddActionButton($"التركيز على: {inv.Name}", (s, ev) => {
                        state.TargetInnovation = inv.Name;
                        ShowEraInnovationMenu(sender, e);
                    });
                }
            }

            AddActionButton("العودة إلى مركز الحكم", ShowGovernanceHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }
        """
        
        # Inject method before ShowCourtHub
        if "private void ShowCourtHub(" in content:
            content = content.replace("private void ShowCourtHub(", method_str + "\\n        private void ShowCourtHub(")
            
    # Add button to GovernanceHub
    target_str = 'AddActionButton($"إدارة الوقت والتاريخ [{state.Time.GetDateString()}]", ShowTimeManagementMenu);'
    new_btn = '            AddActionButton("الابتكارات الثقافية والعصور", ShowEraInnovationMenu);\\n            ' + target_str
    
    if "الابتكارات الثقافية والعصور" not in content and target_str in content:
        content = content.replace(target_str, new_btn)
    
    with io.open('MainForm.cs', 'w', encoding='utf-8') as f:
        f.write(content)

if __name__ == '__main__':
    patch_mainform()
    print("Done")
