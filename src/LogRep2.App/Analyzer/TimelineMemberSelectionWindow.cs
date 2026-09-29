using System.Windows;
using System.Windows.Controls;
using CheckBox = System.Windows.Controls.CheckBox;

namespace FFXI_LogAnalyzer.App;

public sealed partial class TimelineMemberSelectionWindow : Window
{
    private readonly List<CheckBox> _members = [];

    public TimelineMemberSelectionWindow(IEnumerable<(string Name, string Classification)> candidates)
    {
        InitializeComponent();
        foreach (var candidate in candidates)
        {
            var label = new DockPanel { LastChildFill = true };
            var classification = new TextBlock { Text = candidate.Classification, Margin = new Thickness(12, 0, 0, 0) };
            classification.SetResourceReference(StyleProperty, "CaptionTextStyle");
            DockPanel.SetDock(classification, Dock.Right);
            label.Children.Add(classification);
            label.Children.Add(new TextBlock { Text = candidate.Name, TextTrimming = TextTrimming.CharacterEllipsis });
            var check = new CheckBox
            {
                Content = label,
                Tag = candidate.Name,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
                Margin = new Thickness(14, 9, 14, 9),
                ToolTip = candidate.Name,
            };
            check.SetResourceReference(StyleProperty, "AppCheckBoxStyle");
            System.Windows.Automation.AutomationProperties.SetName(check, candidate.Name);
            check.Checked += OnSelectionChanged;
            check.Unchecked += OnSelectionChanged;
            _members.Add(check);
            MemberList.Children.Add(check);
        }
        RefreshSelection();
    }

    public IReadOnlyList<string> SelectedMembers => _members.Where(check => check.IsChecked == true)
        .Select(check => (string)check.Tag).ToArray();

    private void OnSelectionChanged(object sender, RoutedEventArgs e) => RefreshSelection();

    private void RefreshSelection()
    {
        var count = SelectedMembers.Count;
        SelectionCount.Text = $"選択中: {count:N0} / {_members.Count:N0}名";
        AcceptButton.IsEnabled = count > 0;
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;
}