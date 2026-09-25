using FortniteTracker.Core;

namespace FortniteTracker.Core.Tests;

// Lines copied from a real FortniteGame.log (Sept 2026), with account IDs and names replaced.
// When a Fortnite patch breaks detection, paste the new line here and fix the regex.
public class FortniteLogParserTests
{
    public const string Self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1";
    public const string Mate = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb2";

    public static class Lines
    {
        public const string Login =
            $"[2026.09.25-02.38.08:865][499]LogOnlineAccount: Display: [OnlineAccount:index=2:uid=0][process_user_login] Successfully logged in user. UserId=[{Self}] DisplayName=[PlayerOne] EpicAccountId=[MCP:{Self}] AuthTicket=[<Redacted>]";
        public const string MateJoined =
            $"[2026.09.25-02.38.14:975][645]LogVoiceChatManager: [2503] [00007FF0D092F9A0] OnEpicPartyMemberJoined PartyMember=[EpicAccount[FortniteClient]:EpicAccount:2 ({Mate})&MCP[None]:MCP:2 ({Mate})]";
        public const string SelfLeft =
            $"[2026.09.25-03.34.23:025][470]LogVoiceChatManager: [2564] [00007FF0D092F9A0] OnEpicPartyMemberLeft PartyMember=[MCP[None]:MCP:1 ({Self})&EpicAccount[FortniteClient]:EpicAccount:1 ({Self})&Null[None]:Null:2 ({Self})&EpicGame[FortniteClient]:EpicGame:2 (dddddddddddddddddddddddddddddddd)]";
        public const string PartyLeft =
            "[2026.09.25-03.34.23:027][470]LogVoiceChatManager: [2515] [00007FF0D092F9A0] OnEpicPartyLeft Party=[cccccccccccccccccccccccccccccccc]";
        public const string SelfPresence =
            $"[2026.09.25-02.38.30:100][800]LogJoinInProgress: Verbose: [Presence.Parse] user=MCP:{Self} SessionId(empty=True) SessionKey(empty=True) Playlist=Playlist_Habanero_PiperBoot_Duos ServerPlayers=0 InUnjoinable=False LinkResolve=presence-mnemonic Allow";
        public const string FriendPresence =
            "[2026.09.25-02.38.13:607][544]LogJoinInProgress: Verbose: [Presence.Parse] user=MCP:ee3a0...08392 SessionId(empty=True) SessionKey(empty=True) Playlist=Playlist_Habanero_RopeSmile_Solo ServerPlayers=0 InUnjoinable=False LinkResolve=presence-mnemonic Allow";
        public const string Welcomed =
            "[2026.09.25-02.39.05:610][343]LogNet: Welcomed by server (Level: /Game/Athena/Maps/Athena_Empty?VerseURI=/Fortnite.com/GameFeatures/BRRoot+/Fortnite.com/GameFeatures/CreativeRoot?ValidateDownloadableContentDuringTravel, Game: /Game/Athena/Athena_GameMode.Athena_GameMode_C)";
        public const string Placement =
            "[2026.09.25-02.40.59:328][395]LogFortPostGamePlacementOverlay: UPostGamePlacementOverlay::LocalPlacementChanged We now have placement for the local player.";
        public const string SpectateTeammate =
            "[2026.09.25-02.40.19:224][616]LogFortViewTarget: [30f0] trying to set view target from BP_SpectatorPawn_C_2147482644 to Pawn:Mate Name";
        public const string SpectateSelf =
            "[2026.09.25-02.40.26:293][700]LogFortViewTarget: [30f0] trying to set view target from Pawn:PlayerOne to Pawn:PlayerOne";
        public const string SpectateEliminator =
            "[2026.09.25-02.41.04:239][800]LogFortViewTarget: [30f0] trying to set view target from BP_SpectatorPawn_C_2147482643 to Pawn:ライバル Rival 01";
        public const string SpectateEliminatorAgain =
            "[2026.09.25-02.41.04:243][800]LogFortViewTarget: [30f0] trying to set view target from Pawn:ライバル Rival 01 to Pawn:ライバル Rival 01";
        public const string SpectateNext =
            "[2026.09.25-02.41.30:000][900]LogFortViewTarget: [30f0] trying to set view target from Pawn:ライバル Rival 01 to Pawn:Anonyme[272]";
    }

    private static GameEvent? ParseIgnoringTime(string line) => FortniteLogParser.Parse(line) is { } e ? e with { At = default } : null;

    [Fact]
    public void Login_yields_local_player() =>
        Assert.Equal(new LocalPlayerDetected(Self, "PlayerOne"), ParseIgnoringTime(Lines.Login));

    [Fact]
    public void Teammate_join_yields_account_id() =>
        Assert.Equal(new PartyMemberJoined(Mate), ParseIgnoringTime(Lines.MateJoined));

    [Fact]
    public void Member_left_yields_account_id() =>
        Assert.Equal(new PartyMemberLeft(Self), ParseIgnoringTime(Lines.SelfLeft));

    [Fact]
    public void Local_party_left() =>
        Assert.IsType<LocalPartyLeft>(ParseIgnoringTime(Lines.PartyLeft));

    [Fact]
    public void Welcomed_by_server_starts_match() =>
        Assert.Equal(new MatchStarted("/Game/Athena/Maps/Athena_Empty"), ParseIgnoringTime(Lines.Welcomed));

    [Fact]
    public void Placement_ends_match() =>
        Assert.IsType<MatchEnded>(ParseIgnoringTime(Lines.Placement));

    [Fact]
    public void Own_presence_yields_full_id_and_playlist() =>
        Assert.Equal(new PlaylistSeen(Self, "Playlist_Habanero_PiperBoot_Duos"), ParseIgnoringTime(Lines.SelfPresence));

    [Fact]
    public void Friend_presence_keeps_redacted_id() =>
        Assert.Equal(new PlaylistSeen("ee3a0...08392", "Playlist_Habanero_RopeSmile_Solo"), ParseIgnoringTime(Lines.FriendPresence));

    [Fact]
    public void View_target_keeps_names_with_spaces_and_unicode() =>
        Assert.Equal(new ViewTargetChanged("ライバル Rival 01"), ParseIgnoringTime(Lines.SpectateEliminator));

    [Theory]
    [InlineData("[2026.09.25-02.40.14:283][616]LogFortViewTarget: [30f0] trying to set view target from Pawn:PlayerOne to Controller:PlayerOne")]
    [InlineData("[2026.09.25-02.39.33:518][616]LogFortViewTarget: [30f0] trying to set view target from Pawn:PlayerOne to AthenaAircraft_C_2147482646")]
    public void Non_player_view_targets_are_ignored(string line) =>
        Assert.Null(FortniteLogParser.Parse(line));

    [Theory]
    [InlineData("Anonyme[272]", true)]
    [InlineData("Anonymous[1]", true)]
    [InlineData("J o h n D.", false)]
    [InlineData("777.player", false)]
    public void Streamer_mode_names_are_recognized(string name, bool anonymous) =>
        Assert.Equal(anonymous, FortniteLogParser.IsAnonymous(name));

    [Fact]
    public void Timestamp_is_read_as_utc()
    {
        var e = FortniteLogParser.Parse(Lines.Welcomed)!;

        Assert.Equal(new DateTime(2026, 9, 25, 2, 39, 5, 610, DateTimeKind.Utc), e.At);
        Assert.Equal(DateTimeKind.Utc, e.At.Kind);
    }

    [Theory]
    // Opponents only ever appear with redacted IDs; they must not be mistaken for party members.
    [InlineData("[2026.09.25-02.39.09:739][487]LogFort: AFortGameStateAthena::CheckAndAddMissedPlayerStatesToMaps(): Added missed player state to team and squad map, with UniqueId: MCP:9f8e7...6d5c4, in team: 6 and squad: 4.")]
    [InlineData("[2026.09.25-02.38.18:018][661]LogParty: Verbose: Created new party member [MCP:1a2b3...4c5d6, Party (V2:cccccccccccccccccccccccccccccccc-57379872-default)]")]
    [InlineData("[2026.09.25-02.38.08:281][499]LogNet: Browse: /Game/Maps/Frontend?Name=Player")]
    [InlineData("[2026.09.25-02.38.13:607][544]LogJoinInProgress: Verbose: [Presence.Parse] user=MCP:cb2eb...70107 SessionId(empty=True) SessionKey(empty=True) Playlist=None ServerPlayers=0")]
    public void Unrelated_lines_are_ignored(string line) =>
        Assert.Null(FortniteLogParser.Parse(line));
}
