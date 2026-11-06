namespace RuinRunner.Backend
{
    /// <summary>
    /// Local side of the profile flow. The backend coordinator pushes confirmed server
    /// profiles through this interface instead of depending on the save system directly.
    /// </summary>
    public interface IProfileStore
    {
        /// <summary>Path of a pre-server save file for the slot, or null when there is none to import.</summary>
        string ExistingLegacyPath(int slotNumber);

        /// <summary>Binds the server's profile list to the local slots.</summary>
        void BindServerProfiles(ProfileView[] profiles);

        /// <summary>Stores a profile the server has confirmed.</summary>
        void ApplyServerProfile(ProfileView profile);
    }
}
