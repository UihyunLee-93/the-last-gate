using System;
using System.Collections.Generic;

[Serializable]
public class StoryPackage
{
    public string worldName;
    public string logline;
    public List<string> storyBeats;
    public List<Mercenary> mercenaries;
    public string seed;
}

[Serializable]
public class Mercenary
{
    public string id;      // m001~m008
    public string name;
    public string job;
    public string race;
    public string trait;
    public string tagline; 
}