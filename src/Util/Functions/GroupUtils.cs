
namespace DadsEZContainers.Util.Functions;

public class GroupUtils
{
    // Get a list of all excluded groups for a container
    public static List<string> GetExcludedGroups(string container)
    {
        if (DadsEZContainersPlugin.yamlData.TryGetValue(container, out Dictionary<string, List<string>> containerData))
        {
            if (containerData.TryGetValue("exclude", out List<string> excludeList))
            {
                return excludeList.Where(excludeItem => DadsEZContainersPlugin.groups.ContainsKey(excludeItem)).ToList();
            }
        }

        return new List<string>();
    }
    public static bool IsGroupDefined(string groupName)
    {
        if (DadsEZContainersPlugin.yamlData == null)
        {
            DadsEZContainersPlugin.DadsEZContainersLogger.LogError("yamlData is null. Make sure to call DeserializeYamlFile() before using IsGroupDefined.");
            return false;
        }

        bool groupInYaml = false;

        if (DadsEZContainersPlugin.yamlData.TryGetValue("groups", out Dictionary<string, List<string>> groupsData))
        {
            groupInYaml = groupsData.ContainsKey(groupName);
        }

        // Check for the group in both yamlData and predefined groups
        return groupInYaml || DadsEZContainersPlugin.groups.ContainsKey(groupName);
    }


    /*public static bool IsGroupDefined(string groupName)
    {
        if (DadsEZContainersPlugin.yamlData == null)
        {
            DadsEZContainersPlugin.DadsEZContainersLogger.LogError(
                "yamlData is null. Make sure to call DeserializeYamlFile() before using IsGroupDefined.");
            return false;
        }

        if (!DadsEZContainersPlugin.yamlData.ContainsKey("groups"))
        {
            return false;
        }

        var groupsData = DadsEZContainersPlugin.yamlData["groups"] as Dictionary<object, object>;
        if (groupsData == null)
        {
            DadsEZContainersPlugin.DadsEZContainersLogger.LogError(
                "Unable to cast groupsData to Dictionary<object, object>.");
            return false;
        }

        return groupsData.ContainsKey(groupName);
    }*/


// Check if a group exists in the container data
    public static bool GroupExists(string groupName)
    {
        return DadsEZContainersPlugin.groups.ContainsKey(groupName);
    }

// Get a list of all groups in the container data
    public static List<string> GetAllGroups()
    {
        return DadsEZContainersPlugin.groups.Keys.ToList();
    }

// Get a list of all items in a group
    public static List<string> GetItemsInGroup(string groupName)
    {
        if (DadsEZContainersPlugin.groups.TryGetValue(groupName, out HashSet<string> groupPrefabs))
        {
            return groupPrefabs.ToList();
        }

        return new List<string>();
    }

    /*public static bool IsItemInGroup(string itemName, string groupName)
    {
        if (PredefinedGroups.ContainsKey(groupName))
        {
            return PredefinedGroups[groupName].Items.Contains(itemName);
        }

        return false;
    }*/
}