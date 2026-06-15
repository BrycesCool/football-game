@echo off
cd /d "C:\Users\estac\My project (1)"
echo ===ADD=== > "C:\Users\estac\My project (1)\git_push_log.txt" 2>&1
git add -A >> "C:\Users\estac\My project (1)\git_push_log.txt" 2>&1
echo ===COMMIT=== >> "C:\Users\estac\My project (1)\git_push_log.txt" 2>&1
git commit -m "Add extracted stadium materials" >> "C:\Users\estac\My project (1)\git_push_log.txt" 2>&1
echo ===PUSH=== >> "C:\Users\estac\My project (1)\git_push_log.txt" 2>&1
git push >> "C:\Users\estac\My project (1)\git_push_log.txt" 2>&1
echo ===VERIFY=== >> "C:\Users\estac\My project (1)\git_push_log.txt" 2>&1
git status -sb >> "C:\Users\estac\My project (1)\git_push_log.txt" 2>&1
echo ===DONE=== >> "C:\Users\estac\My project (1)\git_push_log.txt" 2>&1
