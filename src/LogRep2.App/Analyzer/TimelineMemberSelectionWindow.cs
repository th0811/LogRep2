using System.Windows;
using System.Windows.Controls;
using CheckBox = System.Windows.Controls.CheckBox;

namespace FFXI_LogAnalyzer.App;

public sealed partial class TimelineMemberSelectionWindow : Window
{
    private readonly List<CheckBox> _members = [];

    private readonly TimelineMemberSelectionViewModel _viewModel;
    private readonly Action<Window>? _manage;
    private readonly Action<string>? _registerPc;
    private readonly Action<string>? _registerNpc;
    private readonly Action<string>? _clear;

    public TimelineMemberSelectionWindow(IEnumerable<(string Name, string Classification)> candidates)
        : this(new TimelineMemberSelectionViewModel(() => candidates.ToArray())) { }

    public TimelineMemberSelectionWindow(TimelineMemberSelectionViewModel viewModel,
        Action<Window>? manage = null, Action<string>? registerPc = null,
        Action<string>? registerNpc = null, Action<string>? clear = null)
    {
        _viewModel = viewModel;
        _manage = manage;
        _registerPc = registerPc;
        _registerNpc = registerNpc;
        _clear = clear;
        InitializeComponent();
        ManageButton.IsEnabled = manage is not null;
        RenderMembers();
    }

    private void RenderMembers()
    {
        _members.Clear();
        MemberList.Children.Clear();
        var selected = _viewModel.SelectedMembers.ToHashSet(StringComparer.Ordinal);
        foreach (var candidate in _viewModel.Candidates)
        {
            var label = new DockPanel { LastChildFill = true };
            if (_registerPc is not null && _registerNpc is not null && _clear is not null)
            {
                var button = new System.Windows.Controls.Button { Content = "登録…", Margin = new Thickness(12, 0, 0, 0) };
                button.SetResourceReference(StyleProperty, "LinkButtonStyle");
                var menu = new ContextMenu();
                void Add(string title, Action<string> action)
                {
                    var item = new MenuItem { Header = title };
                    item.Click += (_, _) => { action(candidate.Name); _viewModel.Refresh(); RenderMembers(); };
                    menu.Items.Add(item);
                }
                Add("PC名として登録", _registerPc);
                Add("NPC名として登録", _registerNpc);
                Add("登録を解除", _clear);
                button.ContextMenu = menu;
                button.Click += (_, e) => { menu.PlacementTarget = button; menu.IsOpen = true; e.Handled = true; };
                DockPanel.SetDock(button, Dock.Right);
                label.Children.Add(button);
            }
            var classification = new TextBlock { Text = candidate.Classification, Margin = new Thickness(12, 0, 0, 0) };
            classification.SetResourceReference(StyleProperty, "CaptionTextStyle");
            DockPanel.SetDock(classification, Dock.Right);
            label.Children.Add(classification);
            label.Children.Add(new TextBlock { Text = candidate.Name, TextTrimming = TextTrimming.CharacterEllipsis });
            var check = new CheckBox
            {
                Content = label,
                Tag = candidate.Name,
                IsChecked = selected.Contains(candidate.Name),
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
        EmptyMessage.Visibility = _members.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshSelection();
    }

    public IReadOnlyList<string> SelectedMembers => _viewModel.SelectedMembers;

    private void OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        var check = (CheckBox)sender;
        _viewModel.SetSelected((string)check.Tag, check.IsChecked == true);
        RefreshSelection();
    }

    private void OnManage(object sender, RoutedEventArgs e)
    {
        _manage?.Invoke(this);
        _viewModel.Refresh();
        RenderMembers();
    }

    private void OnSelectRegistered(object sender, RoutedEventArgs e) => SelectMembers(kind => kind == "PC登録");
    private void OnSelectAll(object sender, RoutedEventArgs e) => SelectMembers(_ => true);
    private void OnClearSelection(object sender, RoutedEventArgs e) => SelectMembers(_ => false);

    private void SelectMembers(Func<string, bool> predicate)
    {
        _viewModel.SelectWhere(predicate);
        RenderMembers();
    }
    private void RefreshSelection()
    {
        var count = SelectedMembers.Count;
        SelectionCount.Text = $"選択中: {count:N0} / {_members.Count:N0}名";
        AcceptButton.IsEnabled = count > 0;
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;
}