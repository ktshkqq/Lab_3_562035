using Godot;
using System;

/// Компонент видимости. Наследуется от Area3D, чтобы обнаруживать
/// объекты в заданном радиусе. Выбирает ближайшую цель из разрешённых групп
/// и передаёт её компонентам стрельбы и движения.
[GlobalClass] // Позволяет добавлять этот компонент через меню "Добавить узел" в редакторе Godot
public partial class VisibilityComponent : Area3D
{
    // Ссылка на родительский узел
    private Node3D parentNode;

    // Текущая выбранная цель для атаки/движения
    private Node3D Target;

    // Ссылки на другие компоненты. [Export] позволяет назначить их прямо в инспекторе Godot
    [Export]
    public ShootComponent shootComponent;
    [Export]
    public MoveComponent moveComponent;

    // Группы, которые этот компонент считает целями (кого "видит" и атакует).
    // Для зелёных башен и игрока: { "Enemy" }.
    // Для красных башен и Crawler: { "Tower", "Core", "Player" }.
    [Export]
    public string[] TargetGroups = { "Enemy" };

    // Список всех подходящих объектов, находящихся в данный момент в зоне видимости
    private Godot.Collections.Array<Node3D> detectedBodies = new Godot.Collections.Array<Node3D>();


    // Вызывается при инициализации узла в дереве сцены.
    public override void _Ready()
    {
        // Получаем родительский узел (считаем, что это Node3D)
        parentNode = GetParent<Node3D>();

        // Проверяем, есть ли у этого компонента дочерний CollisionShape3D
        if (this.GetNodeOrNull<CollisionShape3D>("CollisionShape3D") != null)
        {
            // Подписываемся на сигналы входа и выхода тел из зоны Area3D
            this.BodyEntered += (Node3D body) => OnBodyEntered(body);
            this.BodyExited += (Node3D body) => OnBodyExited(body);

            // Проверяем, не находятся ли уже какие-то тела в зоне при создании (на случай спавна внутри зоны)
            Godot.Collections.Array<Node3D> overlappingBodies = GetOverlappingBodies();
            for (int i = 0; i < overlappingBodies.Count; i++)
                OnBodyEntered(overlappingBodies[i]);
        }
    }


    // Обработчик входа тела в зону видимости.
    // В список целей попадают только тела из TargetGroups, имеющие HealthComponent.
    private void OnBodyEntered(Node3D body)
    {
        // Не добавляем самого владельца компонента и дубликаты
        if (body == parentNode || detectedBodies.Contains(body))
            return;

        // Проверяем, входит ли тело хотя бы в одну из целевых групп
        bool isTarget = false;
        foreach (string group in TargetGroups)
        {
            if (body.IsInGroup(group))
            {
                isTarget = true;
                break;
            }
        }

        if (!isTarget) return;

        // Проверяем, есть ли у тела компонент здоровья (чтобы не атаковать, например, просто стены)
        HealthComponent h = body.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (h != null)
            // Если есть, добавляем его в список потенциальных целей
            detectedBodies.Add(body);
    }

    // Обработчик выхода тела из зоны видимости.
    private void OnBodyExited(Node3D body)
    {
        // Удаляем тело из списка целей, так как оно больше не в зоне поражения
        detectedBodies.Remove(body);
    }

    // Вызывается каждый кадр. Ищет ближайшую цель и передает её другим компонентам.
    public override void _Process(double delta)
    {
        // Убираем из списка уже удалённые объекты (погибшие враги, башни и т.п.)
        for (int i = detectedBodies.Count - 1; i >= 0; i--)
        {
            if (!GodotObject.IsInstanceValid(detectedBodies[i]))
                detectedBodies.RemoveAt(i);
        }

        Node3D closestBody = null;

        // Перебираем все обнаруженные тела, чтобы найти ближайшее
        for (int i = 0; i < detectedBodies.Count; i++)
        {
            // Если closestBody еще не выбран, или если текущее тело ближе, чем предыдущее ближайшее
            if (closestBody == null
                || GlobalPosition.DistanceTo(detectedBodies[i].GlobalPosition)
                   < GlobalPosition.DistanceTo(closestBody.GlobalPosition))
            {
                closestBody = detectedBodies[i]; // Обновляем ближайшую цель
            }
        }

        // Устанавливаем найденную ближайшую цель (если никого нет, будет null)
        Target = closestBody;

        // Передаем цель компоненту стрельбы
        if (shootComponent != null)
        {
            shootComponent.SetTarget(Target);
        }

        // Передаем цель компоненту движения
        if (moveComponent != null)
        {
            moveComponent.SetTarget(Target);
        }
    }
}