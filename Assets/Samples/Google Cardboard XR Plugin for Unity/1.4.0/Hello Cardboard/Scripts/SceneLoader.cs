using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(fileName = "New Scene Loader", menuName = "Scene Loader")]
public class SceneLoader : ScriptableObject
{
    public void LoadScene(string sceneName) => SceneManager.LoadScene(sceneName);
}