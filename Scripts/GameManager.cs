using Godot;

/// Менеджер игры. Прикрепляется к корневому узлу сцены игрового поля.
///  - хранит запас металла игрока и показывает его на экране;
///  - следит за ВСЕМИ узлами, которые удаляются со сцены, и когда удалён
///    уничтоженный враг или башня (группы Enemy / Tower с MetalComponent),
///    автоматически начисляет игроку награду;
///  - принимает оплату за постройку башен и визуально сообщает, если металла не хватает.
[GlobalClass]
public partial class GameManager : Node
{
    // Доступ к менеджеру из других скриптов (например, из Player)
    public static GameManager Instance { get; private set; }

    // Сигнал при изменении количества металла
    [Signal]
    public delegate void MetalChangedEventHandler(int metal);

    // Стартовый запас металла
    [Export]
    public int StartMetal = 50;

    // Текущий запас металла
    public int Metal { get; private set; }

    // Элементы интерфейса (создаются из кода)
    private Label _metalLabel;
    private Label _warningLabel;
    private Tween _warningTween;
    private Tween _metalTween;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        Metal = StartMetal;
        BuildHud();
        UpdateMetalLabel();

        // SceneTree сообщает об удалении КАЖДОГО узла со сцены,
        // поэтому отдельно регистрировать заспавненные объекты не нужно
        GetTree().NodeRemoved += OnNodeRemoved;
    }

    public override void _ExitTree()
    {
        // SceneTree живёт дольше сцены: обязательно отписываемся,
        // иначе после перезапуска сигнал придёт в уже удалённый менеджер
        GetTree().NodeRemoved -= OnNodeRemoved;

        if (Instance == this)
            Instance = null;
    }

    // Вызывается при удалении любого узла из дерева сцены
    private void OnNodeRemoved(Node node)
    {
        // Нас интересуют только враги и башни
        if (!node.IsInGroup("Enemy") && !node.IsInGroup("Tower"))
            return;

        // Награда только за УНИЧТОЖЕННЫЕ объекты (HealthComponent вызывает QueueFree).
        // При перезагрузке сцены дочерние узлы так не помечаются, поэтому
        // перезапуск уровня металл не начисляет.
        if (!node.IsQueuedForDeletion())
            return;

        MetalComponent metal = MetalComponent.FindIn(node);
        if (metal == null)
            return;

        AddMetal(metal.Reward);
    }

    // Начисляет металл
    public void AddMetal(int amount)
    {
        if (amount <= 0) return;

        Metal += amount;
        UpdateMetalLabel();
        EmitSignal(SignalName.MetalChanged, Metal);
    }

    // Хватает ли металла на покупку
    public bool CanAfford(int cost)
    {
        return Metal >= cost;
    }

    // Пытается списать металл. Возвращает true, если оплата прошла.
    // Если металла не хватает — показывает предупреждение на экране.
    public bool TrySpend(int cost)
    {
        if (!CanAfford(cost))
        {
            ShowNotEnoughMetal(cost);
            return false;
        }

        Metal -= cost;
        UpdateMetalLabel();
        EmitSignal(SignalName.MetalChanged, Metal);
        return true;
    }

    // ---------- Интерфейс ----------

    // Создаёт счётчик металла и надпись-предупреждение
    private void BuildHud()
    {
        CanvasLayer layer = new CanvasLayer();
        AddChild(layer);

        _metalLabel = new Label();
        _metalLabel.Position = new Vector2(20, 20);
        _metalLabel.AddThemeFontSizeOverride("font_size", 32);
        layer.AddChild(_metalLabel);

        _warningLabel = new Label();
        _warningLabel.Position = new Vector2(20, 70);
        _warningLabel.AddThemeFontSizeOverride("font_size", 26);
        _warningLabel.Modulate = new Color(1f, 0.2f, 0.2f, 0f); // сначала невидима
        layer.AddChild(_warningLabel);
    }

    private void UpdateMetalLabel()
    {
        if (_metalLabel != null)
            _metalLabel.Text = "Металл: " + Metal;
    }

    // Визуально показывает, что металла не хватает:
    // счётчик вспыхивает красным, а под ним на секунду появляется надпись
    private void ShowNotEnoughMetal(int cost)
    {
        _warningLabel.Text = "Недостаточно металла! Нужно: " + cost;
        _warningLabel.Modulate = new Color(1f, 0.2f, 0.2f, 1f);

        _warningTween?.Kill();
        _warningTween = _warningLabel.CreateTween();
        _warningTween.TweenInterval(0.8);
        _warningTween.TweenProperty(_warningLabel, "modulate:a", 0f, 0.7);

        _metalLabel.Modulate = new Color(1f, 0.2f, 0.2f, 1f);

        _metalTween?.Kill();
        _metalTween = _metalLabel.CreateTween();
        _metalTween.TweenProperty(_metalLabel, "modulate", Colors.White, 0.8);
    }
}