using System.Drawing;
using System.Windows.Forms;

namespace CybersecurityAwarenessChatbot;

public sealed class ChatbotForm : Form
{
    private readonly ChatbotEngine _bot = new();
    private readonly ListBox _chat = new();
    private readonly TextBox _input = new();
    private readonly Label _memory = new();
    private readonly DataGridView _tasksGrid = new();
    private readonly TextBox _taskTitle = new();
    private readonly TextBox _taskDescription = new();
    private readonly TextBox _taskReminder = new();
    private readonly Label _quizQuestion = new();
    private readonly RadioButton[] _quizOptions = new RadioButton[4];
    private readonly ListBox _activityList = new();

    public ChatbotForm()
    {
        Text = "Cybersecurity Awareness Chatbot - Part 3";
        Size = new Size(1060, 720);
        MinimumSize = new Size(920, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(242, 246, 249);
        Font = new Font("Segoe UI", 10F);

        BuildGui();
        AddBot(_bot.GetGreeting());
        AddBot("[ C Y B E R  P R O T E C T I O N ] tasks - quiz - detect - remember - log");
        RefreshAllViews();
    }

    private void BuildGui()
    {
        var title = new Label
        {
            Text = "Cybersecurity Awareness Chatbot",
            Dock = DockStyle.Top,
            Height = 58,
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(25, 55, 76)
        };

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F)
        };

        tabs.TabPages.Add(BuildChatTab());
        tabs.TabPages.Add(BuildTasksTab());
        tabs.TabPages.Add(BuildQuizTab());
        tabs.TabPages.Add(BuildActivityTab());

        Controls.Add(tabs);
        Controls.Add(title);
    }

    private TabPage BuildChatTab()
    {
        var page = new TabPage("Chatbot");
        page.BackColor = Color.FromArgb(242, 246, 249);

        _chat.Dock = DockStyle.Fill;
        _chat.Font = new Font("Segoe UI", 10F);
        _chat.BackColor = Color.White;
        _chat.ForeColor = Color.FromArgb(35, 45, 55);
        _chat.BorderStyle = BorderStyle.None;
        _chat.HorizontalScrollbar = true;

        _memory.Dock = DockStyle.Right;
        _memory.Width = 260;
        _memory.Text = "Memory\nNo saved details yet.";
        _memory.Padding = new Padding(14);
        _memory.ForeColor = Color.FromArgb(35, 45, 55);
        _memory.BackColor = Color.FromArgb(226, 238, 244);

        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(236, 242, 246)
        };

        _input.Dock = DockStyle.Fill;
        _input.Font = new Font("Segoe UI", 10F);
        _input.PlaceholderText = "Try: Add task to enable 2FA in 3 days, start quiz, or show activity log";

        var send = BuildButton("Send", Color.FromArgb(32, 126, 172), Color.White, 96);
        var clear = BuildButton("Clear", Color.FromArgb(213, 225, 232), Color.FromArgb(25, 55, 76), 86);
        send.Dock = DockStyle.Right;
        clear.Dock = DockStyle.Right;

        send.Click += (_, _) => SendMessage();
        clear.Click += (_, _) => ClearChat();
        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
        };

        bottom.Controls.Add(_input);
        bottom.Controls.Add(clear);
        bottom.Controls.Add(send);

        page.Controls.Add(_chat);
        page.Controls.Add(_memory);
        page.Controls.Add(bottom);
        return page;
    }

    private TabPage BuildTasksTab()
    {
        var page = new TabPage("Task Assistant");
        page.BackColor = Color.White;

        _tasksGrid.Dock = DockStyle.Fill;
        _tasksGrid.AllowUserToAddRows = false;
        _tasksGrid.AllowUserToDeleteRows = false;
        _tasksGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _tasksGrid.MultiSelect = false;
        _tasksGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _tasksGrid.BackgroundColor = Color.White;
        _tasksGrid.RowHeadersVisible = false;
        _tasksGrid.Columns.Add("Id", "Id");
        _tasksGrid.Columns["Id"].Visible = false;
        _tasksGrid.Columns.Add("Title", "Title");
        _tasksGrid.Columns.Add("Description", "Description");
        _tasksGrid.Columns.Add("Reminder", "Reminder");
        _tasksGrid.Columns.Add("Status", "Status");

        var editor = new Panel
        {
            Dock = DockStyle.Right,
            Width = 330,
            Padding = new Padding(16),
            BackColor = Color.FromArgb(226, 238, 244)
        };

        var heading = new Label
        {
            Text = "Add Cybersecurity Task",
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(25, 55, 76)
        };

        ConfigureTextBox(_taskTitle, "Title, e.g. Enable two-factor authentication");
        ConfigureTextBox(_taskDescription, "Description");
        ConfigureTextBox(_taskReminder, "Optional reminder, e.g. in 3 days");
        _taskDescription.Multiline = true;
        _taskDescription.Height = 78;

        var add = BuildButton("Add Task", Color.FromArgb(32, 126, 172), Color.White, 0);
        var reminder = BuildButton("Set Reminder", Color.FromArgb(25, 55, 76), Color.White, 0);
        var complete = BuildButton("Mark Complete", Color.FromArgb(68, 140, 105), Color.White, 0);
        var delete = BuildButton("Delete", Color.FromArgb(178, 72, 68), Color.White, 0);

        add.Dock = DockStyle.Top;
        reminder.Dock = DockStyle.Top;
        complete.Dock = DockStyle.Top;
        delete.Dock = DockStyle.Top;
        add.Height = reminder.Height = complete.Height = delete.Height = 36;

        add.Click += (_, _) => AddTaskFromControls();
        reminder.Click += (_, _) => SetReminderFromControls();
        complete.Click += (_, _) => MarkSelectedTaskComplete();
        delete.Click += (_, _) => DeleteSelectedTask();

        editor.Controls.Add(delete);
        editor.Controls.Add(Spacer(8));
        editor.Controls.Add(complete);
        editor.Controls.Add(Spacer(8));
        editor.Controls.Add(reminder);
        editor.Controls.Add(Spacer(8));
        editor.Controls.Add(add);
        editor.Controls.Add(Spacer(14));
        editor.Controls.Add(_taskReminder);
        editor.Controls.Add(LabelFor("Reminder"));
        editor.Controls.Add(Spacer(8));
        editor.Controls.Add(_taskDescription);
        editor.Controls.Add(LabelFor("Description"));
        editor.Controls.Add(Spacer(8));
        editor.Controls.Add(_taskTitle);
        editor.Controls.Add(LabelFor("Title"));
        editor.Controls.Add(heading);

        page.Controls.Add(_tasksGrid);
        page.Controls.Add(editor);
        return page;
    }

    private TabPage BuildQuizTab()
    {
        var page = new TabPage("Mini Game Quiz");
        page.BackColor = Color.White;

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            BackColor = Color.White
        };

        var start = BuildButton("Start Quiz", Color.FromArgb(32, 126, 172), Color.White, 120);
        start.Dock = DockStyle.Top;
        start.Height = 40;
        start.Click += (_, _) =>
        {
            AddBot(_bot.StartQuiz());
            RefreshQuizDisplay();
            RefreshActivityLog();
        };

        _quizQuestion.Dock = DockStyle.Top;
        _quizQuestion.Height = 120;
        _quizQuestion.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _quizQuestion.ForeColor = Color.FromArgb(25, 55, 76);
        _quizQuestion.Text = "Click Start Quiz to begin.";

        var optionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 150,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        for (var i = 0; i < _quizOptions.Length; i++)
        {
            _quizOptions[i] = new RadioButton
            {
                Width = 760,
                Height = 30,
                Font = new Font("Segoe UI", 10F),
                Text = $"Option {i + 1}"
            };
            optionsPanel.Controls.Add(_quizOptions[i]);
        }

        var submit = BuildButton("Submit Answer", Color.FromArgb(68, 140, 105), Color.White, 150);
        submit.Dock = DockStyle.Top;
        submit.Height = 40;
        submit.Click += (_, _) => SubmitQuizAnswer();

        panel.Controls.Add(submit);
        panel.Controls.Add(optionsPanel);
        panel.Controls.Add(_quizQuestion);
        panel.Controls.Add(start);
        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildActivityTab()
    {
        var page = new TabPage("Activity Log");
        page.BackColor = Color.White;

        _activityList.Dock = DockStyle.Fill;
        _activityList.Font = new Font("Segoe UI", 10F);
        _activityList.BorderStyle = BorderStyle.None;
        _activityList.HorizontalScrollbar = true;

        var refresh = BuildButton("Refresh Log", Color.FromArgb(32, 126, 172), Color.White, 120);
        refresh.Dock = DockStyle.Bottom;
        refresh.Height = 44;
        refresh.Click += (_, _) => RefreshActivityLog();

        page.Controls.Add(_activityList);
        page.Controls.Add(refresh);
        return page;
    }

    private void SendMessage()
    {
        var text = _input.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            AddBot("Please type a message first.");
            return;
        }

        _input.Clear();
        _chat.Items.Add("User: " + text);
        AddBot(_bot.Respond(text));
        RefreshAllViews();
    }

    private void ClearChat()
    {
        _chat.Items.Clear();
        AddBot(_bot.GetGreeting());
        RefreshAllViews();
    }

    private void AddTaskFromControls()
    {
        if (string.IsNullOrWhiteSpace(_taskTitle.Text))
        {
            MessageBox.Show("Please enter a task title.", "Task Assistant", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var task = _bot.AddTask(_taskTitle.Text, _taskDescription.Text, _taskReminder.Text);
        AddBot($"Task added: {task.Title}." + (string.IsNullOrWhiteSpace(task.ReminderText) ? " Would you like a reminder?" : $" Reminder set: {task.ReminderText}."));
        _taskTitle.Clear();
        _taskDescription.Clear();
        _taskReminder.Clear();
        RefreshAllViews();
    }

    private void SetReminderFromControls()
    {
        var task = GetSelectedTask();
        if (task == null)
        {
            MessageBox.Show("Select a task first.", "Task Assistant", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_taskReminder.Text))
        {
            MessageBox.Show("Enter a reminder such as 'in 3 days' or 'tomorrow'.", "Task Assistant", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _bot.SetReminder(task.Id, _taskReminder.Text);
        AddBot($"Got it! I will remind you about '{task.Title}' {_taskReminder.Text}.");
        _taskReminder.Clear();
        RefreshAllViews();
    }

    private void MarkSelectedTaskComplete()
    {
        var task = GetSelectedTask();
        if (task == null)
        {
            MessageBox.Show("Select a task first.", "Task Assistant", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _bot.MarkTaskComplete(task.Id);
        AddBot($"Task marked as complete: {task.Title}.");
        RefreshAllViews();
    }

    private void DeleteSelectedTask()
    {
        var task = GetSelectedTask();
        if (task == null)
        {
            MessageBox.Show("Select a task first.", "Task Assistant", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _bot.DeleteTask(task.Id);
        AddBot($"Task deleted: {task.Title}.");
        RefreshAllViews();
    }

    private void SubmitQuizAnswer()
    {
        var selected = Array.FindIndex(_quizOptions, option => option.Checked);
        if (selected < 0)
        {
            MessageBox.Show("Choose an answer first.", "Mini Game Quiz", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var reply = _bot.AnswerQuiz(selected);
        AddBot(reply);
        RefreshQuizDisplay();
        RefreshActivityLog();
    }

    private CyberTask? GetSelectedTask()
    {
        if (_tasksGrid.SelectedRows.Count == 0)
        {
            return null;
        }

        var id = Convert.ToString(_tasksGrid.SelectedRows[0].Cells["Id"].Value);
        return _bot.Tasks.FirstOrDefault(task => task.Id == id);
    }

    private void RefreshAllViews()
    {
        _memory.Text = "Memory\n" + _bot.MemorySummary;
        RefreshTasksGrid();
        RefreshQuizDisplay();
        RefreshActivityLog();
    }

    private void RefreshTasksGrid()
    {
        _tasksGrid.Rows.Clear();
        foreach (var task in _bot.Tasks)
        {
            _tasksGrid.Rows.Add(task.Id, task.Title, task.Description, task.ReminderText ?? "None", task.IsCompleted ? "Complete" : "Pending");
        }
    }

    private void RefreshQuizDisplay()
    {
        var text = _bot.QuizActive ? _bot.GetCurrentQuizQuestion() : "Click Start Quiz to begin.";
        var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        _quizQuestion.Text = lines.FirstOrDefault() ?? text;

        for (var i = 0; i < _quizOptions.Length; i++)
        {
            _quizOptions[i].Checked = false;
            _quizOptions[i].Text = lines.Skip(1).ElementAtOrDefault(i) ?? $"Option {i + 1}";
            _quizOptions[i].Enabled = _bot.QuizActive;
        }
    }

    private void RefreshActivityLog()
    {
        _activityList.Items.Clear();
        foreach (var entry in _bot.ActivityLog.TakeLast(10))
        {
            _activityList.Items.Add($"[{entry.Timestamp:yyyy-MM-dd HH:mm}] {entry.Description}");
        }
    }

    private void AddBot(string message)
    {
        foreach (var line in message.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
        {
            _chat.Items.Add("Chatbot: " + line);
        }

        _chat.TopIndex = Math.Max(0, _chat.Items.Count - 1);
    }

    private static Button BuildButton(string text, Color backColor, Color foreColor, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            BackColor = backColor,
            ForeColor = foreColor,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(4)
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private static void ConfigureTextBox(TextBox box, string placeholder)
    {
        box.Dock = DockStyle.Top;
        box.Height = 34;
        box.Font = new Font("Segoe UI", 10F);
        box.PlaceholderText = placeholder;
    }

    private static Label LabelFor(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(25, 55, 76)
        };
    }

    private void InitializeComponent()
    {

    }

    private static Control Spacer(int height)
    {
        return new Panel { Dock = DockStyle.Top, Height = height };
    }
}
