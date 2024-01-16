using System;
using System.Collections.Generic;
using System.Text;
using Godot;

/// <summary>
/// This is a dictionary guaranteed to have only one of each value and key. 
/// It may be searched either by TFirst or by TSecond, giving a unique answer because it is many to many.
/// </summary>
/// <typeparam name="TFirst">The type of the "key"</typeparam>
/// <typeparam name="TSecond">The type of the "value"</typeparam>
public class BipartiteGraph<TFirst, TSecond>
{
    readonly IDictionary<TFirst, List<TSecond>> firstToSecond = new Dictionary<TFirst, List<TSecond>>();
    readonly IDictionary<TSecond, List<TFirst>> secondToFirst = new Dictionary<TSecond, List<TFirst>>();

    /// <summary>
    /// Tries to add the pair to the bipartite graph.
    /// </summary>
    /// <param name="first"></param>
    /// <param name="second"></param>
    public void Add(TFirst first, TSecond second)
    {
        if (!firstToSecond.ContainsKey(first))
            firstToSecond.Add(first, new List<TSecond>());

        if (!secondToFirst.ContainsKey(second))
        {
            secondToFirst.Add(second, new List<TFirst>());
        }


        firstToSecond[first].Add(second);
        secondToFirst[second].Add(first);

    }


    #region Exception throwing methods

    /// <summary>
    /// Find the TSecond corresponding to the TFirst first
    /// Throws an exception if first is not in the dictionary.
    /// </summary>
    /// <param name="first">the key to search for</param>
    /// <returns>the value corresponding to first</returns>
    public List<TSecond> GetByFirst(TFirst first)
    {
        List<TSecond> second;
        if (!firstToSecond.TryGetValue(first, out second))
            throw new ArgumentException("first");

        return second;
    }

    /// <summary>
    /// Find the TFirst corresponing to the Second second.
    /// Throws an exception if second is not in the dictionary.
    /// </summary>
    /// <param name="second">the key to search for</param>
    /// <returns>the value corresponding to second</returns>
    public List<TFirst> GetBySecond(TSecond second)
    {
        List<TFirst> first;
        if (!secondToFirst.TryGetValue(second, out first))
        {
            throw new ArgumentException($"This {second}\nis not in this: \n{ToString()}");
        }

        return first;
    }


    /// <summary>
    /// Remove the element contained in first.
    /// If first is not in the dictionary, throws an Exception.
    /// </summary>
    /// <param name="first">the key of the record to delete</param>
    public void RemoveFromByFirst(TFirst first, TSecond element)
    {
        List<TSecond> second;
        if (!firstToSecond.TryGetValue(first, out second))
            throw new ArgumentException("first");

        if (!second.Remove(element))
            throw new ArgumentException("No such element");

        secondToFirst.Remove(element);
    }

    public void RemoveKeyFromFirst(TFirst first)
    {
        List<TSecond> seconds;
        if (!firstToSecond.TryGetValue(first, out seconds))
            throw new ArgumentException("first");

        foreach (TSecond second in seconds)
        {
            secondToFirst[second].Remove(first);
        }

        firstToSecond.Remove(first);
    }

    public void RemoveKeyFromSecond(TSecond second)
    {
        List<TFirst> firsts;
        if (!secondToFirst.TryGetValue(second, out firsts))
            throw new ArgumentException("second");

        foreach (TFirst first in firsts)
        {
            firstToSecond[first].Remove(second);
        }

        secondToFirst.Remove(second);
    }


    /// <summary>
    /// Remove the element contained in second.
    /// If second is not in the dictionary, throws an Exception.
    /// </summary>
    /// <param name="second">the key of the record to delete</param>
    public void RemoveBySecond(TSecond second, TFirst element)
    {
        List<TFirst> first;
        if (!secondToFirst.TryGetValue(second, out first))
            throw new ArgumentException("second");

        if (!first.Remove(element))
            throw new ArgumentException("No such element");

        firstToSecond.Remove(element);
    }

    public void InitializeFirst(TFirst first)
    {
        if (firstToSecond.ContainsKey(first))
            throw new ArgumentException("first");

        firstToSecond.Add(first, new List<TSecond>());
    }

    public void InitializeSecond(TSecond second)
    {
        if (secondToFirst.ContainsKey(second))
            throw new ArgumentException("second");

        secondToFirst.Add(second, new List<TFirst>());
    }

    public void MergeBySecond(TSecond newSecond, TSecond oldSecond)
    {
        if (!secondToFirst.ContainsKey(oldSecond))
            throw new ArgumentException("second");

        foreach (TFirst first in secondToFirst[oldSecond])
            Add(first, newSecond);
    }


    #endregion

    
    #region Try methods

    
    /// <summary>
    /// Find the TSecond corresponding to the TFirst first.
    /// Returns false if first is not in the dictionary.
    /// </summary>
    /// <param name="first">the key to search for</param>
    /// <param name="second">the corresponding value</param>
    /// <returns>true if first is in the dictionary, false otherwise</returns>
    public Boolean TryGetByFirst(TFirst first, out List<TSecond> second)
    {
        return firstToSecond.TryGetValue(first, out second);
    }

    /// <summary>
    /// Find the TFirst corresponding to the TSecond second.
    /// Returns false if second is not in the dictionary.
    /// </summary>
    /// <param name="second">the key to search for</param>
    /// <param name="first">the corresponding value</param>
    /// <returns>true if second is in the dictionary, false otherwise</returns>
    public Boolean TryGetBySecond(TSecond second, out List<TFirst> first)
    {
        return secondToFirst.TryGetValue(second, out first);
    }

    #endregion        

    /// <summary>
    /// The number of keys stored in the dictionary
    /// </summary>
    public int Count
    {
        get { return firstToSecond.Count; }
    }

    /// <summary>
    /// Removes all items from the dictionary.
    /// </summary>
    public void Clear()
    {
        firstToSecond.Clear();
        secondToFirst.Clear();
    }

    public override string ToString()
    {
        StringBuilder s = new StringBuilder();
        foreach (var kvp in firstToSecond)
        {
            
            TFirst second = kvp.Key;
            List<TSecond> first = kvp.Value;
    
            
            s.AppendLine($"{second}");

            
            foreach(var element in first)
            {
                s.AppendLine(element.ToString().PadRight(5));
            }
        }

        
        return s.ToString();
    }
}