using DadsEZContainers.Util.Functions;

namespace DadsEZContainers.IContainers;

public class VanillaContainer(Container _container) : IContainer
{
    public int ProcessContainerInventory(string reqName, int totalAmount, int totalRequirement)
    {
        Inventory cInventory = _container.GetInventory();
        if (cInventory == null) return totalAmount;
        int thisAmount = Mathf.Min(cInventory.CountItems(reqName), totalRequirement - totalAmount);

        DadsEZContainersPlugin.DadsEZContainersLogger.LogIfReleaseAndDebugEnable($"(ConsumeResourcesPatch) Container at {_container.transform.position} has {cInventory.CountItems(reqName)}");

        if (thisAmount == 0) return totalAmount;

        for (int i = 0; i < cInventory.GetAllItems().Count; ++i)
        {
            ItemDrop.ItemData item = cInventory.GetItem(i);
            if (item?.m_shared?.m_name != reqName) continue;
            DadsEZContainersPlugin.DadsEZContainersLogger.LogIfReleaseAndDebugEnable($"Container Total Items Count is {cInventory.GetAllItems().Count}");
            DadsEZContainersPlugin.DadsEZContainersLogger.LogIfReleaseAndDebugEnable($"(ConsumeResourcesPatch) Got stack of {item.m_stack} {reqName}");

            int stackAmount = Mathf.Min(item.m_stack, totalRequirement - totalAmount);
            if (stackAmount == item.m_stack)
            {
                DadsEZContainersPlugin.DadsEZContainersLogger.LogIfReleaseAndDebugEnable($"(ConsumeResourcesPatch) Removing item {reqName} from container");
                DadsEZContainersPlugin.DadsEZContainersLogger.LogIfReleaseAndDebugEnable($"Container inventory before removal: {cInventory.GetAllItems().Count}, Item at index {i}: {cInventory.GetItem(i)?.m_shared?.m_name}");

                bool removed = cInventory.RemoveItem(i);
                DadsEZContainersPlugin.DadsEZContainersLogger.LogIfReleaseAndDebugEnable("Removed was " + removed);
                DadsEZContainersPlugin.DadsEZContainersLogger.LogIfReleaseAndDebugEnable($"Container inventory after attempted removal: {cInventory.GetAllItems().Count}");

                --i;
            }
            else
            {
                DadsEZContainersPlugin.DadsEZContainersLogger.LogIfReleaseAndDebugEnable($"(ConsumeResourcesPatch) Removing {stackAmount} {reqName} from container");
                item.m_stack -= stackAmount;
            }

            totalAmount += stackAmount;

            DadsEZContainersPlugin.DadsEZContainersLogger.LogIfReleaseAndDebugEnable($"(ConsumeResourcesPatch) Total amount is now {totalAmount}/{totalRequirement} {reqName}");

            if (totalAmount >= totalRequirement)
            {
                break;
            }
        }

        _container.Save();
        cInventory.Changed();
        DadsEZContainersPlugin.DadsEZContainersLogger.LogIfReleaseAndDebugEnable("Saved container");

        if (totalAmount >= totalRequirement)
        {
            DadsEZContainersPlugin.DadsEZContainersLogger.LogIfReleaseAndDebugEnable($"(ConsumeResourcesPatch) Consumed enough {reqName}");
        }

        return totalAmount;
    }

    public int ItemCount(string name) => _container.GetInventory()?.CountItems(name) ?? 0;

    public void RemoveItem(string name, int amount) => _container.GetInventory()?.RemoveItem(name, amount);
    
    public void Save()
    {
        _container.Save();
        _container.m_inventory?.Changed();
    }

    public Vector3 GetPosition() => _container.transform.position;
    public string GetPrefabName() => Utils.GetPrefabName(_container.gameObject);
    public Inventory GetInventory() => _container.GetInventory();


    public static VanillaContainer Create(Container container) => new(container);
}