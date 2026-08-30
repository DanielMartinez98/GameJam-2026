using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

//One music player that rides along from scene to scene, so the menu, the interrogation room and the
//party each have their own tune. Because it survives the trip between scenes, two scenes that share a
//tune keep it playing without a restart, and moving to a scene with a different tune fades across.
//
//Put one of these on a GameObject in the menu scene (the scene the game opens on) and assign the three
//clips. It makes itself the only one alive, so a second copy carried in by another scene just removes
//itself. A scene it does not know about plays nothing.
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class MusicDirector : MonoBehaviour
{
    //The first director to load keeps playing; any later copy destroys itself the moment it wakes.
    private static MusicDirector instance;

    //A scene's name and the tune it asks for. Named by scene so simply loading a scene is enough to
    //pick the music, the same way the room authors its own writing next to what it is about.
    [System.Serializable]
    public struct SceneTrack
    {
        public string sceneName;
        public AudioClip music;
    }

    [Header("Scene tracks")]
    [SerializeField] private SceneTrack menu = new SceneTrack { sceneName = "MainMenu" };
    [SerializeField] private SceneTrack interrogationRoom = new SceneTrack { sceneName = "MainScene" };
    [SerializeField] private SceneTrack party = new SceneTrack { sceneName = "GameScene" };

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
    //How long the old tune takes to fall away and the new one to come up. Zero cuts straight over.
    [SerializeField] private float fadeSeconds = 1.5f;

    private AudioSource source;
    private AudioClip currentClip;
    private Coroutine fade;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        //Off on its own so it is not dragged down with whatever scene object it was parented to.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.volume = volume;

        SceneManager.sceneLoaded += OnSceneLoaded;
        //Awake happens after the opening scene is already up, so start its tune now rather than waiting
        //for the next load.
        Apply(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply(scene.name);
    }

    //Pick the tune the named scene asks for and, if it is not the one already playing, fade across to
    //it. The same tune two scenes running is left untouched.
    private void Apply(string sceneName)
    {
        AudioClip clip = ClipFor(sceneName);
        if (clip == currentClip)
        {
            return;
        }
        currentClip = clip;

        if (fade != null)
        {
            StopCoroutine(fade);
        }
        fade = StartCoroutine(FadeTo(clip));
    }

    private AudioClip ClipFor(string sceneName)
    {
        if (sceneName == menu.sceneName) return menu.music;
        if (sceneName == interrogationRoom.sceneName) return interrogationRoom.music;
        if (sceneName == party.sceneName) return party.music;
        return null;
    }

    private IEnumerator FadeTo(AudioClip next)
    {
        //Only fade out if something is actually playing, so the very first tune does not sit through a
        //fade of silence before it starts.
        if (source.isPlaying)
        {
            yield return Fade(source.volume, 0f);
            source.Stop();
        }

        if (next == null)
        {
            fade = null;
            yield break;
        }

        source.clip = next;
        source.volume = 0f;
        source.Play();
        yield return Fade(0f, volume);
        fade = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeSeconds <= 0f)
        {
            source.volume = to;
            yield break;
        }
        //Unscaled so a paused game (Time.timeScale 0) does not freeze the music mid-fade.
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(from, to, t / fadeSeconds);
            yield return null;
        }
        source.volume = to;
    }
}
