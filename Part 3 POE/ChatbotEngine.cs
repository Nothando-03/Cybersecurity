using System.Text.Json;
using System.Text.RegularExpressions;

namespace CybersecurityAwarenessChatbot;

public sealed class ChatbotEngine
{
    private readonly Random _random = new();
    private readonly Dictionary<string, List<string>> _responses;
    private readonly Dictionary<string, string[]> _keywords;
    private readonly Dictionary<string, string> _memory = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CyberTask> _tasks = [];
    private readonly List<ActivityLogEntry> _activityLog = [];
    private readonly List<QuizQuestion> _quizQuestions;
    private readonly string _databasePath;
    private string? _lastTopic;
    private int _quizIndex;
    private int _quizScore;
    private bool _quizActive;

    public ChatbotEngine()
    {
        _databasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cybersecurity_tasks_db.json");
        _responses = CreateResponses();
        _keywords = CreateKeywords();
        _quizQuestions = CreateQuizQuestions();
        LoadTasks();
        LogAction("Chatbot started.");
    }

    public IReadOnlyList<CyberTask> Tasks => _tasks;

    public IReadOnlyList<ActivityLogEntry> ActivityLog => _activityLog;

    public bool QuizActive => _quizActive;

    public string MemorySummary
    {
        get
        {
            if (_memory.Count == 0)
            {
                return "No saved details yet.";
            }

            return string.Join(Environment.NewLine, _memory.Select(item => $"{item.Key}: {item.Value}"));
        }
    }

    public string GetGreeting()
    {
        return "Hello! I can chat about cybersecurity, manage tasks and reminders, run a quiz, and show my activity log.";
    }

    public string Respond(string input)
    {
        input = input.Trim();
        var lower = input.ToLowerInvariant();
        Remember(input);
        var sentiment = DetectSentiment(lower);

        if (TryHandleQuizAnswer(lower, out var quizAnswer))
        {
            return quizAnswer;
        }

        if (LooksLikeActivityRequest(lower))
        {
            LogAction("Activity log requested from chat.");
            return GetRecentActivityText();
        }

        if (LooksLikeQuizStart(lower))
        {
            return StartQuiz();
        }

        if (TryCreateTaskFromInput(input, lower, out var taskMessage))
        {
            return sentiment + taskMessage;
        }

        if (TrySetReminderFromInput(input, lower, out var reminderMessage))
        {
            return sentiment + reminderMessage;
        }

        if (LooksLikeTaskListRequest(lower))
        {
            LogAction("Task list requested from chat.");
            return GetTaskSummary();
        }

        var topic = DetectTopic(lower);
        if (IsFollowUp(lower) && _lastTopic != null)
        {
            topic = _lastTopic;
        }

        if (topic != null)
        {
            _lastTopic = topic;
            LogAction($"Cybersecurity topic discussed: {topic}.");
            return sentiment + BuildTopicResponse(topic);
        }

        return sentiment + "I did not quite understand that. You can ask about passwords, scams, privacy, phishing, tasks, reminders, quizzes, or the activity log.";
    }

    public CyberTask AddTask(string title, string description, string? reminderText)
    {
        var task = new CyberTask
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = CleanTitle(title),
            Description = string.IsNullOrWhiteSpace(description) ? CleanTitle(title) : description.Trim(),
            ReminderText = string.IsNullOrWhiteSpace(reminderText) ? null : reminderText.Trim(),
            CreatedAt = DateTime.Now,
            IsCompleted = false
        };

        _tasks.Add(task);
        SaveTasks();
        LogAction($"Task added: {task.Title}{FormatReminderSuffix(task.ReminderText)}.");
        return task;
    }

    public bool SetReminder(string taskId, string reminderText)
    {
        var task = _tasks.FirstOrDefault(item => item.Id == taskId);
        if (task == null)
        {
            return false;
        }

        task.ReminderText = reminderText.Trim();
        SaveTasks();
        LogAction($"Reminder set for task '{task.Title}': {task.ReminderText}.");
        return true;
    }

    public bool MarkTaskComplete(string taskId)
    {
        var task = _tasks.FirstOrDefault(item => item.Id == taskId);
        if (task == null)
        {
            return false;
        }

        task.IsCompleted = true;
        task.CompletedAt = DateTime.Now;
        SaveTasks();
        LogAction($"Task marked complete: {task.Title}.");
        return true;
    }

    public bool DeleteTask(string taskId)
    {
        var task = _tasks.FirstOrDefault(item => item.Id == taskId);
        if (task == null)
        {
            return false;
        }

        _tasks.Remove(task);
        SaveTasks();
        LogAction($"Task deleted: {task.Title}.");
        return true;
    }

    public string StartQuiz()
    {
        _quizActive = true;
        _quizIndex = 0;
        _quizScore = 0;
        LogAction("Cybersecurity quiz started.");
        return GetCurrentQuizQuestion();
    }

    public string AnswerQuiz(int selectedIndex)
    {
        if (!_quizActive)
        {
            return "Start the quiz first, then choose an answer.";
        }

        var question = _quizQuestions[_quizIndex];
        var correct = selectedIndex == question.CorrectIndex;
        if (correct)
        {
            _quizScore++;
        }

        var result = correct ? "Correct!" : $"Not quite. The correct answer is {OptionLetter(question.CorrectIndex)}.";
        var feedback = $"{result} {question.Explanation}";
        _quizIndex++;

        if (_quizIndex >= _quizQuestions.Count)
        {
            _quizActive = false;
            var final = $"\n\nQuiz complete. Your score is {_quizScore}/{_quizQuestions.Count}. {GetScoreMessage()}";
            LogAction($"Cybersecurity quiz completed with score {_quizScore}/{_quizQuestions.Count}.");
            return feedback + final;
        }

        LogAction($"Quiz question answered: {result}");
        return feedback + "\n\n" + GetCurrentQuizQuestion();
    }

    public string GetCurrentQuizQuestion()
    {
        if (!_quizActive)
        {
            return "The quiz is not active. Click Start Quiz or type 'start quiz'.";
        }

        var question = _quizQuestions[_quizIndex];
        var options = question.Options.Select((option, index) => $"{OptionLetter(index)}) {option}");
        return $"Question {_quizIndex + 1}/{_quizQuestions.Count}: {question.Prompt}\n" + string.Join(Environment.NewLine, options);
    }

    public string GetRecentActivityText(int count = 10)
    {
        if (_activityLog.Count == 0)
        {
            return "No activity has been recorded yet.";
        }

        var lines = _activityLog.TakeLast(count).Select((entry, index) => $"{index + 1}. [{entry.Timestamp:yyyy-MM-dd HH:mm}] {entry.Description}");
        return "Here is a summary of recent actions:\n" + string.Join(Environment.NewLine, lines);
    }

    public void LogAction(string description)
    {
        _activityLog.Add(new ActivityLogEntry(DateTime.Now, description));
    }

    private string BuildTopicResponse(string topic)
    {
        var list = _responses[topic];
        var reply = list[_random.Next(list.Count)];

        if (_memory.TryGetValue("Name", out var name))
        {
            reply = $"{name}, {char.ToLowerInvariant(reply[0])}{reply[1..]}";
        }

        if (_memory.TryGetValue("Interest", out var interest) && !topic.Equals(interest, StringComparison.OrdinalIgnoreCase))
        {
            reply += $" Since you are interested in {interest}, remember that it also connects to your wider online safety.";
        }

        return reply;
    }

    private bool TryCreateTaskFromInput(string input, string lower, out string message)
    {
        message = string.Empty;
        if (!ContainsAny(lower, "add task", "new task", "create task", "set task", "remind me", "can you remind me", "set a reminder"))
        {
            return false;
        }

        var title = ExtractTaskTitle(input);
        var reminder = ExtractReminder(input);
        var description = CreateTaskDescription(title);
        var task = AddTask(title, description, reminder);

        message = task.ReminderText == null
            ? $"Task added: '{task.Title}'. Would you like to set a reminder for this task?"
            : $"Reminder set for '{task.Title}' {task.ReminderText}.";
        return true;
    }

    private bool TrySetReminderFromInput(string input, string lower, out string message)
    {
        message = string.Empty;
        if (!ContainsAny(lower, "remind me in", "remind me tomorrow", "set reminder") || _tasks.Count == 0)
        {
            return false;
        }

        var reminder = ExtractReminder(input) ?? input;
        var latestTask = _tasks.Last();
        SetReminder(latestTask.Id, reminder);
        message = $"Got it! I will remind you about '{latestTask.Title}' {reminder}.";
        return true;
    }

    private string GetTaskSummary()
    {
        if (_tasks.Count == 0)
        {
            return "You do not have any saved cybersecurity tasks yet.";
        }

        var lines = _tasks.Select((task, index) => $"{index + 1}. {task.Title} - {(task.IsCompleted ? "Complete" : "Pending")}{FormatReminderSuffix(task.ReminderText)}");
        return "Your cybersecurity tasks:\n" + string.Join(Environment.NewLine, lines);
    }

    private string ExtractTaskTitle(string input)
    {
        var cleaned = Regex.Replace(input, @"(?i)\b(can you\s+)?(please\s+)?(add|create|new|set)\s+(a\s+)?(task|reminder)\s*(to|for|[-:])?", " ");
        cleaned = Regex.Replace(cleaned, @"(?i)\bremind me\s+(to|about)?", " ");
        cleaned = Regex.Replace(cleaned, @"(?i)\b(in \d+\s+(day|days|hour|hours)|tomorrow|next week|today)\b", " ");
        cleaned = cleaned.Trim(' ', '.', ',', ':', '-');
        return string.IsNullOrWhiteSpace(cleaned) ? "Review cybersecurity settings" : cleaned;
    }

    private static string? ExtractReminder(string input)
    {
        var lower = input.ToLowerInvariant();
        var inMatch = Regex.Match(lower, @"\bin\s+\d+\s+(day|days|hour|hours|week|weeks)\b");
        if (inMatch.Success)
        {
            return inMatch.Value;
        }

        if (lower.Contains("tomorrow"))
        {
            return "tomorrow";
        }

        if (lower.Contains("next week"))
        {
            return "next week";
        }

        if (lower.Contains("today"))
        {
            return "today";
        }

        return null;
    }

    private static string CreateTaskDescription(string title)
    {
        return title.ToLowerInvariant() switch
        {
            var text when text.Contains("password") => "Review password strength and enable multi-factor authentication where possible.",
            var text when text.Contains("privacy") => "Review account privacy settings and reduce unnecessary personal data sharing.",
            var text when text.Contains("2fa") || text.Contains("two-factor") => "Enable two-factor authentication on important accounts.",
            var text when text.Contains("phishing") || text.Contains("email") => "Check suspicious messages carefully and report phishing attempts.",
            _ => "Cybersecurity task created from the user's request."
        };
    }

    private string? DetectTopic(string input)
    {
        foreach (var item in _keywords)
        {
            if (item.Value.Any(keyword => input.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return item.Key;
            }
        }

        return null;
    }

    private void Remember(string input)
    {
        var name = Regex.Match(input, @"\b(?:my name is|i am|i'm)\s+([A-Z][a-zA-Z]+)");
        if (name.Success)
        {
            _memory["Name"] = name.Groups[1].Value;
            LogAction($"Remembered user's name: {name.Groups[1].Value}.");
        }

        var interest = Regex.Match(input.ToLowerInvariant(), @"interested in (password|scam|privacy|phishing|malware|browsing|quiz|tasks)");
        if (interest.Success)
        {
            _memory["Interest"] = interest.Groups[1].Value;
            LogAction($"Remembered user's cybersecurity interest: {interest.Groups[1].Value}.");
        }
    }

    private void LoadTasks()
    {
        try
        {
            if (!File.Exists(_databasePath))
            {
                return;
            }

            var json = File.ReadAllText(_databasePath);
            var tasks = JsonSerializer.Deserialize<List<CyberTask>>(json);
            if (tasks != null)
            {
                _tasks.AddRange(tasks);
            }
        }
        catch
        {
            LogAction("Task database could not be loaded; starting with an empty task list.");
        }
    }

    private void SaveTasks()
    {
        var json = JsonSerializer.Serialize(_tasks, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_databasePath, json);
    }

    private bool TryHandleQuizAnswer(string lower, out string answer)
    {
        answer = string.Empty;
        if (!_quizActive)
        {
            return false;
        }

        var selected = lower.Trim() switch
        {
            "a" or "1" or "option a" => 0,
            "b" or "2" or "option b" => 1,
            "c" or "3" or "option c" => 2,
            "d" or "4" or "option d" => 3,
            _ => -1
        };

        if (selected < 0)
        {
            return false;
        }

        answer = AnswerQuiz(selected);
        return true;
    }

    private string GetScoreMessage()
    {
        var percentage = (double)_quizScore / _quizQuestions.Count;
        if (percentage >= 0.8)
        {
            return "Great job! You are a cybersecurity pro.";
        }

        if (percentage >= 0.5)
        {
            return "Good work. Keep learning to stay safe online.";
        }

        return "Keep practising. Cybersecurity gets easier with repetition.";
    }

    private static bool LooksLikeActivityRequest(string input)
    {
        return ContainsAny(input, "activity log", "what have you done", "recent actions", "show log");
    }

    private static bool LooksLikeQuizStart(string input)
    {
        return ContainsAny(input, "start quiz", "take quiz", "mini game", "minigame", "cyber quiz", "quiz me");
    }

    private static bool LooksLikeTaskListRequest(string input)
    {
        return ContainsAny(input, "show tasks", "list tasks", "view tasks", "my tasks", "what tasks");
    }

    private static bool IsFollowUp(string input)
    {
        return ContainsAny(input, "tell me more", "another tip", "explain more", "more details", "what else", "another one");
    }

    private static string DetectSentiment(string input)
    {
        if (ContainsAny(input, "worried", "scared", "overwhelmed", "anxious", "unsure"))
        {
            return "It's completely understandable to feel that way. Let me help step by step. ";
        }

        if (ContainsAny(input, "frustrated", "confused", "stuck", "annoyed"))
        {
            return "I hear that this is frustrating. Here is a simple explanation. ";
        }

        if (ContainsAny(input, "curious", "interested", "wondering"))
        {
            return "Great question. Curiosity is a good cybersecurity habit. ";
        }

        return string.Empty;
    }

    private static bool ContainsAny(string input, params string[] words)
    {
        return words.Any(word => input.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static string CleanTitle(string title)
    {
        return Regex.Replace(title.Trim(), @"\s+", " ");
    }

    private static string FormatReminderSuffix(string? reminder)
    {
        return string.IsNullOrWhiteSpace(reminder) ? "" : $" (reminder: {reminder})";
    }

    private static string OptionLetter(int index)
    {
        return ((char)('A' + index)).ToString();
    }

    private static Dictionary<string, List<string>> CreateResponses()
    {
        return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["password"] =
            [
                "Use strong, unique passwords for every account and avoid personal details.",
                "A password manager can help you store strong passwords safely.",
                "Turn on multi-factor authentication wherever possible."
            ],
            ["scam"] =
            [
                "Be careful with urgent messages asking for money, passwords, or verification codes.",
                "Check the sender and website address before clicking any link.",
                "Never share OTPs or PINs. Real support staff should not ask for them."
            ],
            ["privacy"] =
            [
                "Review your privacy settings and limit who can see your personal information.",
                "Only give apps the permissions they really need.",
                "Avoid posting sensitive details such as your address, school, phone number, or routine."
            ],
            ["phishing"] =
            [
                "Be cautious of emails asking for personal information. Scammers often pretend to be trusted organisations.",
                "Do not open unexpected attachments or links.",
                "Phishing messages often create panic. Slow down and verify the source."
            ],
            ["malware"] =
            [
                "Keep your devices updated and avoid downloading files from untrusted websites.",
                "Malware can steal information or damage files, so scan suspicious downloads before opening them."
            ],
            ["browsing"] =
            [
                "Use secure websites that begin with https, especially for sign-ins and payments.",
                "Avoid entering sensitive details on public Wi-Fi unless you trust the connection."
            ]
        };
    }

    private static Dictionary<string, string[]> CreateKeywords()
    {
        return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["password"] = ["password", "passcode", "login", "credential", "2fa", "two-factor"],
            ["scam"] = ["scam", "fraud", "fake", "otp", "pin", "verification code"],
            ["privacy"] = ["privacy", "private", "personal information", "permissions", "settings"],
            ["phishing"] = ["phishing", "email", "link", "attachment", "spoof"],
            ["malware"] = ["malware", "virus", "trojan", "ransomware", "download"],
            ["browsing"] = ["browser", "browsing", "wifi", "wi-fi", "website", "https"]
        };
    }

    private static List<QuizQuestion> CreateQuizQuestions()
    {
        return
        [
            new("What should you do if an email asks for your password?", ["Reply with your password", "Delete or report the email", "Forward it to friends", "Ignore all emails"], 1, "Legitimate services should not ask for your password by email."),
            new("Which password is strongest?", ["password123", "MyName2005", "Blue!River!91!Table", "12345678"], 2, "Long, unique passwords with mixed characters are harder to guess."),
            new("What does 2FA help protect against?", ["Weak screen brightness", "Unauthorised account access", "Slow internet", "Spam only"], 1, "Two-factor authentication adds another step before someone can access your account."),
            new("True or false: Public Wi-Fi is always safe for online banking.", ["True", "False", "Only at night", "Only on phones"], 1, "Public Wi-Fi can be risky, especially without extra protection."),
            new("A suspicious link should be checked by...", ["Clicking quickly", "Hovering or inspecting the address", "Sharing it", "Downloading it"], 1, "Checking the address can reveal fake or misspelled websites."),
            new("What is phishing?", ["A fake message designed to steal information", "A firewall update", "A password manager", "A backup method"], 0, "Phishing tricks users into revealing sensitive information."),
            new("Which information should you avoid posting publicly?", ["Favourite colour", "Home address and routine", "A general hobby", "Weather opinion"], 1, "Attackers can misuse personal details."),
            new("What should you do with unused app permissions?", ["Leave all enabled", "Remove permissions that are not needed", "Give more access", "Ignore them"], 1, "Limiting permissions reduces privacy risk."),
            new("Ransomware usually tries to...", ["Encrypt files and demand payment", "Improve battery life", "Organise photos", "Update your browser"], 0, "Ransomware locks or encrypts data to pressure victims."),
            new("True or false: Reusing one password across accounts is safe.", ["True", "False", "Only for school", "Only for games"], 1, "If one account is compromised, reused passwords put other accounts at risk."),
            new("A good first step after spotting a scam is to...", ["Send money", "Slow down and verify the source", "Share your PIN", "Install unknown software"], 1, "Scams often rely on panic and urgency."),
            new("Why are updates important?", ["They only change colours", "They can fix security weaknesses", "They delete passwords", "They stop all emails"], 1, "Updates often patch vulnerabilities attackers could exploit.")
        ];
    }
}

public sealed class CyberTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ReminderText { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
}

public sealed record ActivityLogEntry(DateTime Timestamp, string Description);

public sealed record QuizQuestion(string Prompt, string[] Options, int CorrectIndex, string Explanation);
