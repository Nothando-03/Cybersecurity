# Cybersecurity Awareness Chatbot GUI - Part 3

This is a C# Windows Forms GUI application for the Cybersecurity Awareness Chatbot POE.

## Implemented features

- Part 1 and Part 2 chatbot behaviour: keyword recognition, random responses, memory, sentiment detection, and fallback responses.
- Task Assistant tab for adding, viewing, deleting, completing, and setting reminders for cybersecurity tasks.
- Local task storage in `cybersecurity_tasks_db.json` so tasks remain available after restarting the app.
- Cybersecurity mini-game quiz with 12 questions, multiple-choice answers, immediate feedback, explanations, and final score.
- NLP simulation through keyword and phrase detection for requests such as `add task`, `remind me`, `start quiz`, and `show activity log`.
- Activity Log tab that records tasks, reminders, quiz activity, NLP interactions, and chatbot actions.

## How to run

1. Open `CybersecurityAwarenessChatbot.csproj` in Visual Studio.
2. Make sure .NET 8 is installed on your PC.
3. Click `Start`.

## Try these chatbot commands

- Add task to enable two-factor authentication in 3 days
- Remind me to update my password tomorrow
- Show tasks
- Start quiz
- A
- Show activity log
- What have you done for me?
- I am worried about phishing emails
