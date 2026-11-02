using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Bounded 2D voices, independent cosmetic randomness and authored clips.</summary>
    public sealed class CombatAudio : MonoBehaviour
    {
        static CombatAudio instance;
        static readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        static readonly System.Random variation=new System.Random(5429);
        readonly AudioSource[] voices=new AudioSource[16];
        readonly bool[] paused=new bool[16],interfaceVoice=new bool[16];
        readonly float[] started=new float[16];
        static GameSettingsService settings;
        public static void Configure(GameSettingsService value){settings=value;}
        public static AudioClip Clip(string id)
        {
            if(!clips.TryGetValue(id,out var clip)){clip=Resources.Load<AudioClip>("Audio/Combat/"+id);clips[id]=clip;}
            return clip;
        }
        static CombatAudio Ensure()
        {
            if(instance!=null)return instance;
            var root=new GameObject("CombatAudioVoices");instance=root.AddComponent<CombatAudio>();
            for(var i=0;i<instance.voices.Length;i++)
            {
                var child=new GameObject("Voice"+i);child.transform.SetParent(root.transform,false);
                var voice=child.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;voice.dopplerLevel=0;
                instance.voices[i]=voice;
            }
            return instance;
        }
        public static void PlayClip(AudioClip clip,AudioChannel channel,float volume,float pitch=1,bool pauseWithGame=false)
        {
            if(clip==null)return;
            var ui=!pauseWithGame&&(channel==AudioChannel.Interface||channel==AudioChannel.Inventory);
            if(!ui&&Time.timeScale<=0)return;
            var mix=settings==null?1:settings.GetVolume(channel);if(mix<=0)return;
            var pool=Ensure();var slot=-1;
            for(var i=0;i<pool.voices.Length;i++)if(!pool.voices[i].isPlaying&&!pool.paused[i]){slot=i;break;}
            // Hit floods never restart a louder attack or grow an unbounded set of AudioSources.
            if(slot<0)return;
            var source=pool.voices[slot];source.clip=clip;source.volume=Mathf.Clamp01(volume*mix);
            source.pitch=pitch;pool.interfaceVoice[slot]=ui;pool.started[slot]=Time.unscaledTime;source.Play();
        }
        public static void Native(string id,AudioChannel channel,float volume,float pitch=1,bool pauseWithGame=false)
        {
            // Source recording levels differ; compensate before applying the user's channel/master volumes.
            var gain=id=="bow"?2.4f:id=="reward-rare"?1.9f:1;
            PlayClip(Clip(id),channel,Mathf.Clamp01(volume*gain),pitch*(.96f+(float)variation.NextDouble()*.08f),pauseWithGame);
        }
        public static void PlayWeapon(WeaponFamily family,bool special=false,string weaponId=null)
        {
            var id=family==WeaponFamily.Dagger?"dagger":family==WeaponFamily.Longsword?"sword":family==WeaponFamily.Katana?"katana":
                family==WeaponFamily.Greatsword?"cleave":family==WeaponFamily.Mace?"mace":family==WeaponFamily.Bow?"bow":
                family==WeaponFamily.Crossbow?"crossbow":family==WeaponFamily.Shotgun?"shotgun":family==WeaponFamily.Cannon?"cannon":
                family==WeaponFamily.Rifle?"rifle":"pistol";
            Native(id,AudioChannel.Player,special?.52f:.42f,special?.90f:1);
            // Enchanted rounds retain an arcane layer; physical actions use
            // their own bowstring, blade, or powder recording.
            if(weaponId=="void_revolver")Native("magic",AudioChannel.Player,special?.20f:.11f,.78f);
        }
        public static void PlayImpact(bool heavy)=>Native(heavy?"heavy-impact":"impact",AudioChannel.Monster,heavy?.45f:.24f);
        public static void StopCombat()
        {
            if(instance==null)return;
            for(var i=0;i<instance.voices.Length;i++)if(!instance.interfaceVoice[i]){instance.voices[i].Stop();instance.paused[i]=false;}
        }
        void Update()
        {
            for(var i=0;i<voices.Length;i++)
            {
                if(interfaceVoice[i])continue;
                if(Time.timeScale<=0&&!paused[i]&&voices[i].isPlaying){voices[i].Pause();paused[i]=true;}
                else if(Time.timeScale>0&&paused[i]){voices[i].UnPause();paused[i]=false;}
            }
        }
    }
}
