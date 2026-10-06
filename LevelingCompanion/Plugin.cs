using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using LevelingCompanion.Windows;

namespace LevelingCompanion;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/levelingcompanion";

    private readonly WindowSystem windows = new("LevelingCompanion");

    private readonly MainWindow mainWindow;

    private readonly Learner learner;

    private readonly Watcher watcher;

    private readonly ClickRecorder recorder = new();

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);

        Configuration config = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.learner = new Learner(new SkillPrompt(), config);
        this.watcher = new Watcher(config, this.learner);

        this.mainWindow = new MainWindow(config, this.watcher, this.learner, this.recorder);
        this.windows.AddWindow(this.mainWindow);

        Svc.Commands.AddHandler(Command, new CommandInfo((_, _) => this.mainWindow.Toggle())
        {
            HelpMessage = "Open the Leveling Companion window.",
        });
        Svc.PluginInterface.UiBuilder.Draw += this.windows.Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi += this.mainWindow.Toggle;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += this.mainWindow.Toggle;
    }

    public void Dispose()
    {
        this.watcher.Dispose();
        this.learner.Dispose();
        this.recorder.Dispose();
        Svc.Commands.RemoveHandler(Command);
        Svc.PluginInterface.UiBuilder.Draw -= this.windows.Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= this.mainWindow.Toggle;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= this.mainWindow.Toggle;
        this.windows.RemoveAllWindows();
        ECommonsMain.Dispose();
    }
}
