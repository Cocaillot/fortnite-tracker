using FortniteTracker.Core;

namespace FortniteTracker.Core.Tests;

// Lines copied from a real FortniteGame.log (Sept 2026), with account IDs and names replaced.
// When a Fortnite patch breaks detection, paste the new line here and fix the regex.
public class FortniteLogParserTests
{
    private const string Self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1";
    private const string Mate = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb2";

    [Fact]
    public void Login_yields_local_player()
    {
        var e = FortniteLogParser.Parse(
            $"[2026.09.25-02.38.08:865][499]LogOnlineAccount: Display: [OnlineAccount:index=2:uid=0][process_user_login] Successfully logged in user. UserId=[{Self}] DisplayName=[PlayerOne] EpicAccountId=[MCP:{Self}] AuthTicket=[<Redacted>]");

        Assert.Equal(new LocalPlayerDetected(Self, "PlayerOne"), e);
    }

    [Fact]
    public void Teammate_join_yields_account_id()
    {
        var e = FortniteLogParser.Parse(
            $"[2026.09.25-02.38.14:975][645]LogVoiceChatManager: [2503] [00007FF0D092F9A0] OnEpicPartyMemberJoined PartyMember=[EpicAccount[FortniteClient]:EpicAccount:2 ({Mate})&MCP[None]:MCP:2 ({Mate})]");

        Assert.Equal(new PartyMemberJoined(Mate), e);
    }

    [Fact]
    public void Member_left_yields_account_id()
    {
        var e = FortniteLogParser.Parse(
            $"[2026.09.25-03.34.23:025][470]LogVoiceChatManager: [2564] [00007FF0D092F9A0] OnEpicPartyMemberLeft PartyMember=[MCP[None]:MCP:1 ({Self})&EpicAccount[FortniteClient]:EpicAccount:1 ({Self})&Null[None]:Null:2 ({Self})&EpicGame[FortniteClient]:EpicGame:2 (dddddddddddddddddddddddddddddddd)]");

        Assert.Equal(new PartyMemberLeft(Self), e);
    }

    [Fact]
    public void Local_party_left()
    {
        var e = FortniteLogParser.Parse(
            "[2026.09.25-03.34.23:027][470]LogVoiceChatManager: [2515] [00007FF0D092F9A0] OnEpicPartyLeft Party=[cccccccccccccccccccccccccccccccc]");

        Assert.IsType<LocalPartyLeft>(e);
    }

    [Fact]
    public void Welcomed_by_server_starts_match()
    {
        var e = FortniteLogParser.Parse(
            "[2026.09.25-02.39.05:610][343]LogNet: Welcomed by server (Level: /Game/Athena/Maps/Athena_Empty?VerseURI=/Fortnite.com/GameFeatures/BRRoot+/Fortnite.com/GameFeatures/CreativeRoot?ValidateDownloadableContentDuringTravel, Game: /Game/Athena/Athena_GameMode.Athena_GameMode_C)");

        Assert.Equal(new MatchStarted("/Game/Athena/Maps/Athena_Empty"), e);
    }

    [Fact]
    public void Placement_ends_match()
    {
        var e = FortniteLogParser.Parse(
            "[2026.09.25-02.40.59:328][395]LogFortPostGamePlacementOverlay: UPostGamePlacementOverlay::LocalPlacementChanged We now have placement for the local player.");

        Assert.IsType<MatchEnded>(e);
    }

    [Theory]
    // Opponents only ever appear with redacted IDs; they must not be mistaken for party members.
    [InlineData("[2026.09.25-02.39.09:739][487]LogFort: AFortGameStateAthena::CheckAndAddMissedPlayerStatesToMaps(): Added missed player state to team and squad map, with UniqueId: MCP:9f8e7...6d5c4, in team: 6 and squad: 4.")]
    [InlineData("[2026.09.25-02.38.18:018][661]LogParty: Verbose: Created new party member [MCP:1a2b3...4c5d6, Party (V2:cccccccccccccccccccccccccccccccc-57379872-default)]")]
    [InlineData("[2026.09.25-02.38.08:281][499]LogNet: Browse: /Game/Maps/Frontend?Name=Player")]
    public void Unrelated_lines_are_ignored(string line)
    {
        Assert.Null(FortniteLogParser.Parse(line));
    }
}
