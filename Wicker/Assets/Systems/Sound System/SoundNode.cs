using System;
using System.Collections.Generic;
using UnityEngine;

public enum SoundNodeType { Sound, Container }
public enum PlayMode { Random, Sequential, Shuffle, WeightedRandom }

[CreateAssetMenu(fileName = "NewSoundNode", menuName = "Audio/Sound Node")]
public class SoundNode : ScriptableObject
{
    [Header("Node Information")]
    public string nodeID; // Unique identifier for this node
    public SoundNodeType nodeType = SoundNodeType.Container;

    [Header("Sound Settings (Only for Sound type nodes)")]
    public AudioClip clip;
    [Range(0f, 1f)] public float baseVolume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
    [Range(0f, 0.5f)] public float pitchVariation = 0f;
    [Range(0f, 0.5f)] public float volumeVariation = 0f;
    public bool loop = false;
    public SoundCategory category = SoundCategory.SFX;
    [Range(0f, 10f)] public float weight = 1f; // For weighted random selection

    [Header("Container Settings (Only for Container type nodes)")]
    public PlayMode playMode = PlayMode.Random;
    public List<SoundNode> childNodes = new List<SoundNode>();

    // Runtime data for sequential/shuffle modes
    private int lastPlayedIndex = -1;
    private List<int> shuffleIndices = new List<int>();
    private int shuffleIndex = 0;
    private Dictionary<string, SoundNode> childNodeDict;

    public void Initialize()
    {
        childNodeDict = new Dictionary<string, SoundNode>();
        foreach (var child in childNodes)
        {
            if (child != null && !string.IsNullOrEmpty(child.nodeID))
            {
                childNodeDict[child.nodeID] = child;
                child.Initialize(); // Recursively initialize children
            }
        }
    }

    public SoundNode GetChildNode(string childID = null)
    {
        if (nodeType != SoundNodeType.Container) return null;

        if (childNodeDict == null) Initialize();

        // If no specific child requested, use play mode logic
        if (string.IsNullOrEmpty(childID))
        {
            return GetNextNode();
        }

        // Get specific child by ID
        if (childNodeDict.TryGetValue(childID, out var child))
        {
            return child;
        }

        // Child not found, try to find recursively
        foreach (var childNode in childNodes)
        {
            if (childNode.nodeType == SoundNodeType.Container)
            {
                var found = childNode.GetChildNode(childID);
                if (found != null) return found;
            }
        }

        return null;
    }

    public List<SoundNode> GetAllChildNodes(bool includeContainers = false)
    {
        List<SoundNode> results = new List<SoundNode>();
        CollectChildNodes(this, results, includeContainers);
        return results;
    }

    public SoundNode GetNextNode()
    {
        if (nodeType != SoundNodeType.Container || childNodes.Count == 0) return null;

        switch (playMode)
        {
            case PlayMode.Random:
                return childNodes[UnityEngine.Random.Range(0, childNodes.Count)];

            case PlayMode.WeightedRandom:
                return GetWeightedRandomNode();

            case PlayMode.Sequential:
                lastPlayedIndex = (lastPlayedIndex + 1) % childNodes.Count;
                return childNodes[lastPlayedIndex];

            case PlayMode.Shuffle:
                if (shuffleIndices.Count == 0 || shuffleIndex >= shuffleIndices.Count)
                {
                    // Generate new shuffle
                    shuffleIndices.Clear();
                    for (int i = 0; i < childNodes.Count; i++) shuffleIndices.Add(i);

                    // Fisher-Yates shuffle
                    for (int i = 0; i < shuffleIndices.Count; i++)
                    {
                        int temp = shuffleIndices[i];
                        int randomIndex = UnityEngine.Random.Range(i, shuffleIndices.Count);
                        shuffleIndices[i] = shuffleIndices[randomIndex];
                        shuffleIndices[randomIndex] = temp;
                    }
                    shuffleIndex = 0;
                }

                return childNodes[shuffleIndices[shuffleIndex++]];

            default:
                return childNodes[0];
        }
    }

    private SoundNode GetWeightedRandomNode()
    {
        float totalWeight = 0f;
        foreach (var node in childNodes)
        {
            totalWeight += Mathf.Max(node.weight, 0.001f); // Avoid zero weight
        }

        float randomPoint = UnityEngine.Random.Range(0f, totalWeight);

        foreach (var node in childNodes)
        {
            if (randomPoint < node.weight)
            {
                return node;
            }
            randomPoint -= node.weight;
        }

        return childNodes[childNodes.Count - 1]; // Fallback
    }

    // Utility method to get all sound nodes in the hierarchy
    public List<SoundNode> GetAllSoundNodes()
    {
        var results = new List<SoundNode>();
        CollectSoundNodes(this, results);
        return results;
    }

    private void CollectSoundNodes(SoundNode node, List<SoundNode> collection)
    {
        if (node.nodeType == SoundNodeType.Sound)
        {
            collection.Add(node);
        }
        else
        {
            foreach (var child in node.childNodes)
            {
                if (child != null)
                {
                    CollectSoundNodes(child, collection);
                }
            }
        }
    }

    private void CollectChildNodes(SoundNode node, List<SoundNode> collection, bool includeContainers)
    {
        if (node == null) return;

        if (includeContainers || node.nodeType == SoundNodeType.Sound)
        {
            collection.Add(node);
        }

        if (node.nodeType == SoundNodeType.Container)
        {
            foreach (var child in node.childNodes)
            {
                CollectChildNodes(child, collection, includeContainers);
            }
        }
    }

    public SoundNode FindNodeInSubtree(string nodeID)
    {
        if (this.nodeID == nodeID) return this;

        if (nodeType == SoundNodeType.Container)
        {
            foreach (var child in childNodes)
            {
                if (child != null)
                {
                    var found = child.FindNodeInSubtree(nodeID);
                    if (found != null) return found;
                }
            }
        }

        return null;
    }

    public string GetPath(SoundNode root)
    {
        return GetPathRecursive(root, this, "");
    }

    private string GetPathRecursive(SoundNode current, SoundNode target, string currentPath)
    {
        if (current == target)
        {
            return string.IsNullOrEmpty(currentPath) ? current.nodeID : currentPath + "/" + current.nodeID;
        }

        if (current.nodeType == SoundNodeType.Container)
        {
            foreach (var child in current.childNodes)
            {
                if (child != null)
                {
                    string childPath = string.IsNullOrEmpty(currentPath) ? current.nodeID : currentPath + "/" + current.nodeID;
                    string result = GetPathRecursive(child, target, childPath);
                    if (!string.IsNullOrEmpty(result)) return result;
                }
            }
        }

        return null;
    }

    ////////// Debug Tree Printing /////////////
    // Add this to SoundNode if you want the simplest possible version:
    public void PrintBasicTree()
    {
        Debug.Log(GetBasicTreeString());
    }

    public string GetBasicTreeString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("Sound Node Tree Structure:");
        sb.AppendLine("==========================");
        BuildTreeString(this, "", true, sb);
        return sb.ToString();
    }

    private void BuildTreeString(SoundNode node, string indent, bool isLast, System.Text.StringBuilder sb)
    {
        if (node == null) return;

        // Current node
        sb.Append(indent);
        sb.Append(isLast ? "'--- " : "|--- ");
        sb.Append(node.nodeID);

        if (node.nodeType == SoundNodeType.Sound)
        {
            sb.Append(" (Sound)");
            if (node.clip != null)
                sb.Append($" [{node.clip.name}]");
        }
        else
        {
            sb.Append($" (Container: {node.playMode})");
        }

        sb.AppendLine();

        // Children
        if (node.nodeType == SoundNodeType.Container)
        {
            string childIndent = indent + (isLast ? "    " : "|   ");

            for (int i = 0; i < node.childNodes.Count; i++)
            {
                bool childIsLast = (i == node.childNodes.Count - 1);
                BuildTreeString(node.childNodes[i], childIndent, childIsLast, sb);
            }
        }
    }
}