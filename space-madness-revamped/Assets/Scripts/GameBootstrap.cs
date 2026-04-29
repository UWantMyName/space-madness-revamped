using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    public ChapterParser chapterParser;
    private void Start() => chapterParser.StartChapter();
}
