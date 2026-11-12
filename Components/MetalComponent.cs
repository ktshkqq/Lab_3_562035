using Godot;

/// Компонент металла. Дочерний узел башни или врага, хранит экономические параметры
/// этого объекта прямо в его сцене:
///  - Cost: сколько металла нужно игроку, чтобы построить эту башню;
///  - Reward: сколько металла получает игрок, когда объект уничтожен.
[GlobalClass]
public partial class MetalComponent : Node
{
    // Стоимость постройки (для вражеских объектов можно оставить 0)
    [Export]
    public int Cost = 20;

    // Награда за уничтожение объекта
    [Export]
    public int Reward = 10;

    // Ищет MetalComponent среди дочерних узлов переданного объекта (null, если нет)
    public static MetalComponent FindIn(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is MetalComponent metal)
                return metal;
        }

        return null;
    }
}
