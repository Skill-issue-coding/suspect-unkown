using System;

// One line of a job's take for the debrief: an item kind, how many were taken and what they were worth
// together, with every cash pickup grouped into one line. A struct with no arrays, so copies never share
// data and JsonUtility writes it as plain values
[Serializable]
public struct JobLoot
{
    public string name;
    public int count;
    public int value;
    public bool objective;
}