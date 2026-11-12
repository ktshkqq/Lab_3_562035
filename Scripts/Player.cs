using Godot;

/// Игрок. Корневой узел сцены игрока (CharacterBody3D).
///  - ходит по карте на WASD и поворачивается к курсору мыши;
///  - стреляет по ЛКМ или пробелу;
///  - имеет здоровье (дочерний узел HealthComponent) и может погибнуть;
///  - ставит башни: клавиша E показывает "призрачную" башню перед игроком,
///    клавиши 1-9 или Tab выбирают тип, повторное E устанавливает башню,
///    Esc отменяет постановку.
[GlobalClass]
public partial class Player : CharacterBody3D
{
    // ---------- Движение ----------
    [ExportGroup("Движение")]
    [Export] public float Speed = 8f;

    // ---------- Стрельба ----------
    [ExportGroup("Стрельба")]
    // Сцена пули игрока (в ней должен быть DamagingComponent с TargetGroups = Enemy)
    [Export] public PackedScene BulletScene;
    [Export] public float BulletDamage = 10f;
    [Export] public float BulletSpeed = 50f;
    // Выстрелов в секунду
    [Export] public float FireRate = 4f;
    // Насколько далеко перед игроком появляется пуля
    [Export] public float MuzzleOffset = 1.5f;
    // На какой высоте над позицией игрока вылетает пуля
    [Export] public float MuzzleHeight = 0.4f;

    // ---------- Башни ----------
    [ExportGroup("Башни")]
    // Сцены башен, которые можно ставить (порядок = номер клавиши 1, 2, 3...)
    [Export] public Godot.Collections.Array<PackedScene> TowerScenes = new Godot.Collections.Array<PackedScene>();
    // Расстояние от игрока до точки установки
    [Export] public float PlaceDistance = 4f;
    // Высота (координата Y), на которой стоят башни
    [Export] public float TowerHeight = 0f;
    // Цвет и прозрачность "призрачной" башни
    [Export] public Color GhostColor = new Color(0.3f, 1f, 0.3f, 0.4f);
    // Цвет призрака, когда металла на эту башню не хватает
    [Export] public Color NoMetalColor = new Color(1f, 0.2f, 0.2f, 0.4f);

    // Время до следующего выстрела
    private float _cooldown = 0f;

    // Текущая "призрачная" башня (null, если режим постановки выключен)
    private Node3D _ghost;

    // Индекс выбранного типа башни
    private int _towerIndex = 0;

    // Материал призрака (меняет цвет в зависимости от того, хватает ли металла)
    private StandardMaterial3D _ghostMaterial;

    // Стоимость выбранной башни (берётся из MetalComponent в её сцене)
    private int _ghostCost = 0;

    public override void _Ready()
    {
        // Группа нужна, чтобы враги (VisibilityComponent, DamagingComponent) считали игрока целью
        AddToGroup("Player");

        RegisterInputActions();
    }

    // Вызывается при удалении игрока (смерть, перезапуск сцены)
    public override void _ExitTree()
    {
        CancelGhost();
    }

    // Создаёт действия ввода из кода, чтобы не настраивать InputMap вручную.
    // PhysicalKeycode не зависит от раскладки: WASD работает и в русской раскладке.
    private static void RegisterInputActions()
    {
        AddKeyAction("move_left", Key.A);
        AddKeyAction("move_right", Key.D);
        AddKeyAction("move_forward", Key.W);
        AddKeyAction("move_back", Key.S);
        AddKeyAction("build", Key.E);

        if (!InputMap.HasAction("shoot"))
        {
            InputMap.AddAction("shoot");
            InputMap.ActionAddEvent("shoot", new InputEventMouseButton { ButtonIndex = MouseButton.Left });
            InputMap.ActionAddEvent("shoot", new InputEventKey { PhysicalKeycode = Key.Space });
        }
    }

    private static void AddKeyAction(string action, Key key)
    {
        // Если действие уже настроено в Project Settings — не трогаем
        if (InputMap.HasAction(action)) return;

        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    public override void _PhysicsProcess(double delta)
    {
        Move();
        AimAtMouse();
        HandleShooting((float)delta);
        UpdateGhost();
    }

    // Одиночные нажатия: постановка башни, выбор типа, отмена
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("build"))
        {
            // Первое нажатие — показать призрак, второе — поставить башню
            if (_ghost == null) StartGhost();
            else PlaceTower();
            return;
        }

        // Выбор типа и отмена работают только пока показан призрак
        if (_ghost == null) return;

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.PhysicalKeycode == Key.Escape)
            {
                CancelGhost();
            }
            else if (key.PhysicalKeycode == Key.Tab)
            {
                SelectTower((_towerIndex + 1) % TowerScenes.Count);
            }
            else if (key.PhysicalKeycode >= Key.Key1 && key.PhysicalKeycode <= Key.Key9)
            {
                SelectTower((int)key.PhysicalKeycode - (int)Key.Key1);
            }
        }
    }

    // ---------- Движение и прицеливание ----------

    // Перемещение по плоскости WASD. Вперёд (W) = -Z, как и у остальных объектов игры
    private void Move()
    {
        Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Velocity = new Vector3(input.X * Speed, 0f, input.Y * Speed);
        MoveAndSlide();
    }

    // Поворот игрока к точке под курсором мыши
    private void AimAtMouse()
    {
        Camera3D camera = GetViewport().GetCamera3D();
        if (camera == null) return;

        // Луч из камеры через курсор пересекаем с горизонтальной плоскостью на высоте игрока
        Vector2 mouse = GetViewport().GetMousePosition();
        Vector3 from = camera.ProjectRayOrigin(mouse);
        Vector3 dir = camera.ProjectRayNormal(mouse);

        Plane ground = new Plane(Vector3.Up, GlobalPosition.Y);
        Vector3? hit = ground.IntersectsRay(from, dir);
        if (hit == null) return;

        Vector3 target = hit.Value;
        target.Y = GlobalPosition.Y;

        // Не поворачиваемся, если курсор почти над самим игроком (LookAt выдаст ошибку)
        if (target.DistanceTo(GlobalPosition) > 0.1f)
            LookAt(target, Vector3.Up);
    }

    // ---------- Стрельба ----------

    private void HandleShooting(float delta)
    {
        _cooldown -= delta;

        // Пока идёт постановка башни, стрелять нельзя (ЛКМ/пробел не должны тратить патроны впустую)
        if (_ghost != null) return;

        if (Input.IsActionPressed("shoot") && _cooldown <= 0f)
        {
            Shoot();
            _cooldown = FireRate > 0f ? 1f / FireRate : 1f;
        }
    }

    private void Shoot()
    {
        if (BulletScene == null)
        {
            GD.Print("[!] BulletScene не назначена у игрока!");
            return;
        }

        Bullet bullet = BulletScene.Instantiate<Bullet>();
        GetTree().CurrentScene.AddChild(bullet);

        // Пуля появляется перед игроком и летит в направлении его взгляда (-Z)
        Vector3 forward = -GlobalTransform.Basis.Z;
        bullet.GlobalPosition = GlobalPosition + forward * MuzzleOffset + Vector3.Up * MuzzleHeight;
        bullet.GlobalRotation = GlobalRotation;

        bullet.SetSpeed(BulletSpeed);

        // Передаём урон в компонент урона пули (ищем по типу, имя узла не важно)
        foreach (Node child in bullet.GetChildren())
        {
            if (child is DamagingComponent damage)
            {
                damage.Damage = BulletDamage;
                break;
            }
        }
    }

    // ---------- Постановка башен ----------

    // Включает режим постановки: создаёт призрак выбранной башни
    private void StartGhost()
    {
        if (TowerScenes.Count == 0)
        {
            GD.Print("[!] У игрока не назначены TowerScenes!");
            return;
        }

        _towerIndex = Mathf.Clamp(_towerIndex, 0, TowerScenes.Count - 1);
        CreateGhost();
    }

    // Меняет тип башни и пересоздаёт призрак
    private void SelectTower(int index)
    {
        if (index < 0 || index >= TowerScenes.Count) return;
        if (TowerScenes[index] == null) return;
        if (index == _towerIndex) return;

        _towerIndex = index;

        if (GodotObject.IsInstanceValid(_ghost))
            _ghost.QueueFree();

        CreateGhost();
    }

    private void CreateGhost()
    {
        PackedScene scene = TowerScenes[_towerIndex];
        if (scene == null)
        {
            GD.Print("[!] TowerScenes[", _towerIndex, "] пуст!");
            return;
        }

        _ghost = BuildGhost(scene);
        GetTree().CurrentScene.AddChild(_ghost);
        UpdateGhost();
    }

    // Устанавливает настоящую башню на место призрака и выключает режим постановки
    private void PlaceTower()
    {
        PackedScene scene = TowerScenes[_towerIndex];
        if (scene == null || !GodotObject.IsInstanceValid(_ghost))
        {
            CancelGhost();
            return;
        }

        // Платим за башню. Если металла не хватает, GameManager покажет предупреждение,
        // а режим постановки остаётся включённым: можно выбрать башню подешевле (Tab, 1-9)
        GameManager manager = GameManager.Instance;
        if (manager != null && !manager.TrySpend(_ghostCost))
            return;

        Node3D tower = scene.Instantiate<Node3D>();
        GetTree().CurrentScene.AddChild(tower);
        tower.GlobalPosition = _ghost.GlobalPosition;

        CancelGhost();
    }

    // Убирает призрак и выключает режим постановки
    private void CancelGhost()
    {
        if (GodotObject.IsInstanceValid(_ghost))
            _ghost.QueueFree();

        _ghost = null;
    }

    // Призрак следует за игроком: стоит на расстоянии PlaceDistance перед ним
    private void UpdateGhost()
    {
        if (!GodotObject.IsInstanceValid(_ghost)) return;

        Vector3 forward = -GlobalTransform.Basis.Z;
        forward.Y = 0f;
        forward = forward.Normalized();

        Vector3 pos = GlobalPosition + forward * PlaceDistance;
        _ghost.GlobalPosition = new Vector3(pos.X, TowerHeight, pos.Z);

        // Призрак красный, если металла не хватает, и обычного цвета, если хватает
        if (_ghostMaterial != null)
        {
            GameManager manager = GameManager.Instance;
            bool affordable = manager == null || manager.CanAfford(_ghostCost);
            _ghostMaterial.AlbedoColor = affordable ? GhostColor : NoMetalColor;
        }
    }

    // Строит призрак из сцены башни: оставляет только меши и заменяет их материал
    // на полупрозрачный. Остальные узлы (коллизии, скрипты, компоненты) в призрак
    // не попадают, поэтому он не стреляет, не сталкивается и не получает урон.
    private Node3D BuildGhost(PackedScene scene)
    {
        // Сцена создаётся, но НЕ добавляется в дерево, поэтому _Ready у её скриптов
        // не вызывается. Мы только читаем из неё меши.
        Node source = scene.Instantiate();

        StandardMaterial3D material = new StandardMaterial3D
        {
            AlbedoColor = GhostColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };

        Node3D ghost = new Node3D();
        CollectMeshes(source, Transform3D.Identity, ghost, material);

        // Запоминаем материал (для смены цвета) и стоимость башни из её сцены
        _ghostMaterial = material;
        MetalComponent metal = MetalComponent.FindIn(source);
        _ghostCost = metal != null ? metal.Cost : 0;

        // Исходная копия сцены больше не нужна
        source.Free();

        return ghost;
    }

    // Рекурсивно обходит узлы сцены и копирует каждый меш в призрак вместе с позицией
    // относительно корня башни. Позицию считаем вручную: узел не в дереве.
    private void CollectMeshes(Node node, Transform3D parentTransform, Node3D ghost, Material material)
    {
        Transform3D transform = parentTransform;
        if (node is Node3D node3D)
            transform = parentTransform * node3D.Transform;

        if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null && meshInstance.Visible)
        {
            MeshInstance3D copy = new MeshInstance3D
            {
                Mesh = meshInstance.Mesh,
                MaterialOverride = material,
                Transform = transform
            };
            ghost.AddChild(copy);
        }

        foreach (Node child in node.GetChildren())
            CollectMeshes(child, transform, ghost, material);
    }
}