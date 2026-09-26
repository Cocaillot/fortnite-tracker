using System.Globalization;

namespace FortniteTracker.Core;

/// <summary>
/// Translations for text produced outside the web UI (notifications, tray menu, overlay, Discord
/// recap). Keys are the English text; untranslated text stays in English.
/// </summary>
public static class Loc
{
    /// <summary>"en" or "fr"; set from the user's language setting.</summary>
    public static string Language { get; set; } = "en";

    public static bool French => Language == "fr";

    public static CultureInfo Culture => French ? CultureInfo.GetCultureInfo("fr-FR") : CultureInfo.InvariantCulture;

    public static string T(string english) => French && Fr.TryGetValue(english, out var fr) ? fr : english;

    /// <summary>Translates "{name}"-style placeholders after lookup: T("{0} ranked up", name).</summary>
    public static string T(string english, params object[] args) => string.Format(Culture, T(english), args);

    // Rank and mode names come from RankNames/PlaylistNames in English; these swap the words.
    private static readonly (string En, string Fr)[] Words =
    [
        ("Silver", "Argent"), ("Gold", "Or"), ("Platinum", "Platine"), ("Diamond", "Diamant"),
        ("Elite", "Élite"), ("Tournament", "Tournoi"), ("Unranked", "Non classé"), ("Ranked", "Classé"),
        ("Squads", "Section"), ("Trios", "Trio"), ("Duos", "Duo"), ("Build", "Construction"),
        ("Zero Build", "Zéro construction"), ("Creative", "Créatif"),
    ];

    /// <summary>A shortcut as shown to the user: "Ctrl+Shift+F", or "Ctrl+Maj+F" in French.</summary>
    public static string Keys(string keys) => French ? keys.Replace("Shift", "Maj") : keys;

    public static string Name(string english)
    {
        if (!French) return english;
        var s = english;
        foreach (var (en, fr) in Words.OrderByDescending(w => w.En.Length)) s = s.Replace(en, fr, StringComparison.Ordinal);
        return s;
    }

    private static readonly Dictionary<string, string> Fr = new()
    {
        // Tray
        ["Show / hide  ({0})"] = "Afficher / masquer  ({0})",
        ["In-game overlay  ({0})"] = "Overlay en jeu  ({0})",
        ["Restart to update"] = "Redémarrer pour mettre à jour",
        ["Restart to update to {0}"] = "Redémarrer pour passer à la {0}",
        ["Exit"] = "Quitter",
        ["Still running"] = "Toujours actif",
        ["Fortnite Tracker keeps tracking from the tray. Right-click the icon to exit."] =
            "Fortnite Tracker continue depuis la barre des tâches. Clic droit sur l'icône pour quitter.",
        ["Update ready"] = "Mise à jour prête",
        ["Version {0} is downloaded. It installs the next time the app restarts."] =
            "La version {0} est téléchargée. Elle s'installe au prochain redémarrage de l'app.",
        // Eliminations
        ["Eliminated by {0}"] = "Éliminé par {0}",
        ["Streamer Mode player"] = "Joueur en mode streamer",
        ["Streamer Mode: name hidden"] = "Mode streamer : nom masqué",
        ["Stats private"] = "Stats privées",
        ["No stats found, likely a bot"] = "Aucune stat, sûrement un bot",
        ["Loading stats…"] = "Chargement des stats…",
        ["Add your API key to see stats"] = "Ajoute ta clé API pour voir les stats",
        ["Stats unavailable"] = "Stats indisponibles",
        ["K/D {0:0.00} · {1:0.#}% wins · {2} matches"] = "K/D {0:0.00} · {1:0.#} % de victoires · {2} parties",
        ["{0:0.0}× your K/D"] = "{0:0.0}× ton K/D",
        ["{0:0.0}× lower K/D than you"] = "K/D {0:0.0}× plus faible que toi",
        ["Bot?"] = "Bot ?",
        ["Casual"] = "Occasionnel",
        ["Average"] = "Moyen",
        ["Skilled"] = "Doué",
        ["Sweat"] = "Tryhard",
        ["Squad member"] = "Coéquipier",
        // Ranks
        ["Rank up! {0}"] = "Promotion ! {0}",
        ["Rank down: {0}"] = "Rétrogradation : {0}",
        ["{0} ranked up"] = "{0} est monté de rang",
        ["{0} reached {1} in {2}"] = "{0} a atteint {1} en {2}",
        ["A friend"] = "Un ami",
        // Notes
        ["You met {0} again"] = "Tu recroises {0}",
        ["is in your party"] = "est dans ton groupe",
        ["eliminated you"] = "t'a éliminé",
        ["is in this match"] = "est dans cette partie",
        // Overlay
        ["ELIMINATED BY"] = "ÉLIMINÉ PAR",
        ["In lobby"] = "Dans le salon",
        // Discord Rich Presence
        ["solo"] = "solo",
        ["squad of {0}"] = "groupe de {0}",
        ["K/D {0:0.00} · {1:0.#}% wins"] = "K/D {0:0.00} · {1:0.#} % de victoires",
        ["squad K/D {0:0.00}"] = "K/D du groupe {0:0.00}",
        ["Tracking stats"] = "Suivi des stats",
        // Export and backup
        ["Date"] = "Date",
        ["Start"] = "Début",
        ["Mode"] = "Mode",
        ["Playlist"] = "Playlist",
        ["Party size"] = "Taille du groupe",
        ["Result"] = "Résultat",
        ["Duration (min)"] = "Durée (min)",
        ["Eliminated by"] = "Éliminé par",
        ["Eliminator K/D"] = "K/D de l'éliminateur",
        ["Victory"] = "Victoire",
        ["Left early"] = "Quittée",
        ["Eliminated"] = "Éliminé",
        ["Saved {0}"] = "{0} enregistré",
        ["That file isn't a Fortnite Tracker backup."] = "Ce fichier n'est pas une sauvegarde Fortnite Tracker.",
        ["Couldn't save the file: {0}"] = "Impossible d'enregistrer le fichier : {0}",
        // Discord recap
        ["Session recap · {0}"] = "Récap de session · {0}",
        ["{0} played {1} {2} ({3}), mostly {4}."] = "{0} a joué {1} {2} ({3}), surtout en {4}.",
        ["match"] = "partie",
        ["matches"] = "parties",
        ["Matches"] = "Parties",
        ["Wins"] = "Victoires",
        ["Kills"] = "Éliminations",
        ["Rank"] = "Rang",
        ["Nemesis"] = "Némésis",
        ["Add a Discord webhook link first."] = "Ajoute d'abord un lien de webhook Discord.",
        ["No session to post yet."] = "Aucune session à publier pour l'instant.",
        ["Already posted."] = "Déjà publiée.",
        ["Discord refused the post ({0}). Check the webhook link."] = "Discord a refusé la publication ({0}). Vérifie le lien du webhook.",
        ["Couldn't reach Discord. Check your connection."] = "Impossible de joindre Discord. Vérifie ta connexion.",
        ["Posted to Discord ✓"] = "Publié sur Discord ✓",
        ["That isn't a Discord webhook link. It starts with https://discord.com/api/webhooks/"] =
            "Ce n'est pas un lien de webhook Discord. Il commence par https://discord.com/api/webhooks/",
    };
}
