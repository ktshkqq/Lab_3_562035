using Godot;

/// Ядро базы. При гибели ядра игра заканчивается:
/// показывается надпись "GAME OVER", игра ставится на паузу,
/// а кнопка перезапускает уровень.
public partial class Core : StaticBody3D
{
    // Защита от повторного вызова конца игры
    private bool _gameOver = false;

    public override void _Ready()
    {
        // Ядро должно быть в группе Core, чтобы враги считали его целью
        AddToGroup("Core");

        // Подписываемся на сигнал смерти из HealthComponent
        HealthComponent health = GetNode<HealthComponent>("HealthComponent");
        health.Died += OnCoreDestroyed;
    }

    // Вызывается, когда здоровье ядра упало до нуля
    private void OnCoreDestroyed()
    {
        if (_gameOver) return;
        _gameOver = true;

        GD.Print("Ядро уничтожено. Конец игры.");
        ShowGameOverScreen();

        // Ставим игру на паузу (физика, враги, башни, пули)
        GetTree().Paused = true;
    }

    // Создаёт экран конца игры прямо из кода, чтобы не требовалась отдельная сцена
    private void ShowGameOverScreen()
    {
        // CanvasLayer рисуется поверх 3D-сцены.
        // ProcessMode.Always — чтобы кнопка работала на паузе.
        CanvasLayer layer = new CanvasLayer();
        layer.ProcessMode = ProcessModeEnum.Always;

        // Контейнер на весь экран, по центру
        CenterContainer center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);

        VBoxContainer box = new VBoxContainer();
        center.AddChild(box);

        Label label = new Label();
        label.Text = "GAME OVER";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", 64);
        box.AddChild(label);

        Button restart = new Button();
        restart.Text = "Начать заново";
        // Ядро к этому моменту уже удалено (QueueFree в HealthComponent),
        // поэтому обработчик не должен обращаться к this — берём SceneTree заранее.
        SceneTree tree = GetTree();
        restart.Pressed += () =>
        {
            // Убираем экран "GAME OVER" вместе с кнопкой
            layer.QueueFree();
            tree.Paused = false;
            tree.ReloadCurrentScene();
        };
        box.AddChild(restart);

        // Добавляем в корень дерева, а не в ядро: ядро может быть удалено
        GetTree().Root.AddChild(layer);
    }
}