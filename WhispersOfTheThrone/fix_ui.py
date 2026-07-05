import codecs
import re

file_path = 'MainForm.cs'

with open(file_path, 'rb') as f:
    raw = f.read(2)
enc = 'utf-16-le' if raw == b'\xff\xfe' else 'utf-8'

with open(file_path, 'r', encoding=enc) as f:
    content = f.read()

# Fix 1: Remove duplicate meeting button
content = re.sub(r'AddActionButton\("عقد اجتماع لجميع الوزراء \?\?\?",[^\)]+\)\);\r?\n', '', content)
content = content.replace('AddActionButton("الجلسات والاجتماعات الذكية", (s, evt) => ShowAiMeetingHub());', 'AddActionButton("مركز الجلسات والاجتماعات الذكية", (s, evt) => ShowAiMeetingHub());')

# Fix 2: Better Wife Names
# Since I can't use a non-existent method, I will use a local static array in the lambda, or just random from a short list inline.
# Example: 
# string[] names = {"عائشة", "فاطمة", "خديجة", "زينب", "مريم", "سلمى", "ليلى", "نورة", "سارة", "هند", "شجرة الدر", "الخيزران", "زبيدة"};
# Name = names[new Random().Next(names.Length)] + " من عائلة " + gov.Name
# But wait, what if the string is just "أميرة {n.Name}"? Let's check how the exact text was.
# From earlier: Name = $"أميرة {n.Name}",
# Let's replace: Name = $"أميرة {n.Name}", with:
replacement1 = 'Name = new string[] {"عائشة", "فاطمة", "خديجة", "زينب", "مريم", "سلمى", "ليلى", "نورة", "سارة", "هند"}[new Random().Next(10)] + " من " + n.Name,'
content = content.replace('Name = $"أميرة {n.Name}",', replacement1)

# And for Governors, it might be: Name = $"ابنة الوالي {gov.Name}",
replacement2 = 'Name = new string[] {"عائشة", "فاطمة", "خديجة", "زينب", "مريم", "سلمى", "ليلى", "نورة", "سارة", "هند"}[new Random().Next(10)] + " آل " + gov.Name.Replace("الوالي ", "").Replace("والي ", ""),'
content = content.replace('Name = $"ابنة الوالي {gov.Name}",', replacement2)
# Or maybe it was `Name = $"السيدة {gov.Name}",`
content = content.replace('Name = $"السيدة من عائلة {gov.Name}",', replacement2)


with open(file_path, 'w', encoding=enc) as f:
    f.write(content)
