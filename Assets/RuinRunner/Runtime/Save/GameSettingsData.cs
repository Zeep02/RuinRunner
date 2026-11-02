using System;

namespace RuinRunner
{
    public enum GameAction
    {
        MoveLeft = 0,
        MoveRight = 1,
        Jump = 2,
        Attack = 3,
        SpecialAttack = 4,
        Dash = 5,
        Interact = 6,
        Inventory = 7,
        Stats = 8,
        Legacy = 9,
        Weapon1 = 10,
        Weapon2 = 11,
        Weapon3 = 12,
        Reload = 13,
        Pause = 14,
        Guard = 15,
        DropDown = 16,
        // Serialized action values are append-only.  Legacy side-view actions
        // above remain reserved so existing settings files keep their meaning.
        MoveUp = 17,
        MoveDown = 18,
        Map = 19,
        Reward4 = 20,
        Reward5 = 21,
        Reward6 = 22,
        Dismantle = 23
    }

    public enum AudioChannel
    {
        Master,
        Music,
        Interface,
        Ambience,
        Player,
        Monster,
        Inventory,
        // Legacy channel values remain stable; their sounds now share Effects volume.
        Effects
    }

    [Serializable]
    public sealed class InputBindingData
    {
        public GameAction action;
        public UnityEngine.KeyCode primary;
        public UnityEngine.KeyCode alternate;

        public InputBindingData(GameAction inputAction, UnityEngine.KeyCode primaryKey, UnityEngine.KeyCode alternateKey = UnityEngine.KeyCode.None)
        {
            action = inputAction;
            primary = primaryKey;
            alternate = alternateKey;
        }
    }
}
