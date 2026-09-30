using UnityEngine;
using Volley.Bootstrap;
using Volley.Sim;

namespace Volley.View
{
    /// <summary>Plays a sound for each simulation event, with volume driven by the event strength.</summary>
    public class AudioView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;

        private AudioSource _source;
        private AudioClip _serveClip, _passClip, _setClip, _attackClip, _blockClip, _floorClip, _whistleClip;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;

            // a dry sound with no reflections is what reads as synthetic; the reverb places it in a gym
            var reverb = gameObject.AddComponent<AudioReverbFilter>();
            reverb.reverbPreset = AudioReverbPreset.Arena;

            //                        name       duration  freq  hardness  decay
            _serveClip   = Sfx.Hit("serve",      0.30f, 150f, 0.75f, 18f);
            _passClip    = Sfx.Hit("pass",       0.20f, 190f, 0.50f, 26f);
            _setClip     = Sfx.Hit("set",        0.16f, 280f, 0.35f, 34f);
            _attackClip  = Sfx.Hit("attack",     0.35f, 130f, 0.90f, 15f);
            _blockClip   = Sfx.Hit("block",      0.30f, 150f, 0.80f, 18f);
            _floorClip   = Sfx.Hit("floor",      0.45f,  95f, 0.60f, 12f);
            _whistleClip = Sfx.Whistle();
        }

        private void Start()
        {
            if (root != null) root.Sim.OnEvent += OnSimEvent;
        }

        private void OnDestroy()
        {
            if (root != null && root.Sim != null) root.Sim.OnEvent -= OnSimEvent;
        }

        private void OnSimEvent(SimEventKind kind, Vector3 position, float strength)
        {
            AudioClip clip = null;
            float volume = Mathf.Clamp01(0.30f + strength * 0.70f);

            switch (kind)
            {
                case SimEventKind.Serve:      clip = _serveClip;  break;
                case SimEventKind.Pass:       clip = _passClip;   break;
                case SimEventKind.Set:        clip = _setClip;    break;
                case SimEventKind.Attack:     clip = _attackClip; break;
                case SimEventKind.Block:      clip = _blockClip;  break;
                case SimEventKind.BallLanded: clip = _floorClip;  break;
                case SimEventKind.Point:      clip = _whistleClip; volume = 0.65f; break;
            }

            if (clip != null) _source.PlayOneShot(clip, volume);
        }
    }
}
