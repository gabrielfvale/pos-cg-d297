using UnityEngine;

[System.Serializable]
public class AuthorData
{
    public string name;
    public string university;
    public Sprite photo;
}

[System.Serializable]
public class PaperData
{
    public string title;
    public string eventName;
    public string abstractText;
    public string type;
    public AuthorData[] authors;
}
