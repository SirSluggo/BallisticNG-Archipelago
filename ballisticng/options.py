from dataclasses import dataclass
from Options import Choice,Toggle,DefaultOnToggle,PerGameCommonOptions

class RequiredMedal(Choice):
    """BallisticNG Archipelago

    Race through the AGL 2159 campaign to send items to players across the
    multiworld and receive unlocks from their checks.

    Campaign invitations, track permits, speed licenses, ships, and ship
    appearances are shuffled into the item pool. You begin with a random
    ship and access to the starting Breeze events.

    Goal — AGL Champion:
    Claim the AGL crown at the Zen Zeus Invitational.
    Unlock AGL 2159 Finale A, B, and C in the Zen Zeus campaign,
    then take 1st place overall in all three tournaments.
    Each finale requires its own Entry item and the appropriate
    track, speed, and ship access.

    Required Medal:
    Every supported event has Bronze, Silver, and Gold Archipelago checks.
    A higher medal also awards any lower checks you have not earned.
    Choose the highest medal that may hold progression for any player.
    Checks above this target are optional rewards and cannot hold progression.
    This changes required results, not the game's difficulty setting.
    The three AGL 2159 Finale tournaments in Zen Zeus always require 1st place
    overall, regardless of this setting."""
    display_name='Required Medal'
    option_bronze=1
    option_silver=2
    option_gold=3
    default=3

class AiPreset(Choice):
    """Choose how tough AI opponents are during normal-mode AP campaign events.
    Standard (recommended): keeps the early campaigns familiar and eases later
    opponents so more of your unlocked ships can compete.
    Relaxed: gentler opponents throughout, for a more casual run.
    Original: keeps the game's usual event difficulty, for the original challenge.
    These presets do not change native hard mode or solo medal targets."""
    display_name='AI Preset'
    option_original=0
    option_relaxed=1
    option_standard=2
    default=2

class BarrelRolls(DefaultOnToggle):
    """Add barrel rolls to AP campaign events for an extra way to boost.
    Roll while airborne using your roll controls or a quick left-right-left /
    right-left-right steering input. Rolls spend shield and reward a successful
    landing with a short boost. Enabled by default for fun; never required to finish."""
    display_name='Barrel Rolls'

class GoldenRacePasses(DefaultOnToggle):
    """Earn one local Golden Race Pass for each event's first Platinum in this seed.
    Spend a pass after countdown to clear one event or the current tournament round.
    Passes are not shuffled items. Assisted clears cannot earn a Platinum pass."""
    display_name='Golden Race Passes'

class PassesInFinales(Toggle):
    """Allow Golden Race Passes in the three finale tournaments.
    Each pass clears only the current round. Disabled by default."""
    display_name='Passes in Finales'

class PassesInSurvival(Toggle):
    """Allow Golden Race Passes to clear a Survival event at Gold and end the run.
    Disabled by default."""
    display_name='Passes in Survival'

class Traps(Toggle):
    """Replace every Pit Crew Encouragement item with a random trap.
    Choose which traps can appear below. Each enabled trap appears at least
    once; the remaining copies are random. Turning traps off keeps the blank
    items, which do nothing. Enable at least one trap type when traps are on.
    Traps wait until racing begins and activate one at a time."""
    display_name='Traps'
    default=0

class Trap0(DefaultOnToggle):
    """Swap your left and right airbrakes for 8 seconds."""
    display_name='Airbrake Mix-Up Trap'

class Trap1(DefaultOnToggle):
    """Stretch your view to an extreme wide angle for 8 seconds."""
    display_name='"Gotta Go Fast!" Trap'

class Trap2(DefaultOnToggle):
    """Lose 30 shield, stopping at 1 so the trap cannot destroy your ship."""
    display_name='Pit Crew Miscalculation Trap'

class Trap3(DefaultOnToggle):
    """Lock your camera to cockpit view for 15 seconds."""
    display_name='Cockpit Lockdown Trap'

class Trap4(DefaultOnToggle):
    """Make you look behind your ship for 3 seconds."""
    display_name='He’s Right Behind Me, Isn’t He? Trap'

class Trap5(DefaultOnToggle):
    """Display random game concept art near the center of your screen for 5 seconds."""
    display_name='Art Appreciation Trap'

class Trap6(DefaultOnToggle):
    """"The Game's Playing Itself Jon"
    Let autopilot take the wheel for 8 seconds."""
    display_name='Backseat Gaming Trap'

class Trap7(DefaultOnToggle):
    """Give the race a low-resolution, retro CRT look for 20 seconds."""
    display_name='Running in the 90s Trap'

class Trap8(DefaultOnToggle):
    """Mirror the race view for 10 seconds. Your HUD and controls stay unchanged."""
    display_name='Mirror Trap'

@dataclass
class BallisticNGOptions(PerGameCommonOptions):
    required_medal: RequiredMedal
    ai_preset: AiPreset
    barrel_rolls: BarrelRolls
    golden_race_passes: GoldenRacePasses
    passes_in_finales: PassesInFinales
    passes_in_survival: PassesInSurvival
    traps: Traps
    airbrake_mix_up_trap: Trap0
    gotta_go_fast_trap: Trap1
    pit_crew_miscalculation_trap: Trap2
    cockpit_lockdown_trap: Trap3
    hes_right_behind_me_trap: Trap4
    art_appreciation_trap: Trap5
    backseat_gaming_trap: Trap6
    running_in_the_90s_trap: Trap7
    mirror_trap: Trap8
