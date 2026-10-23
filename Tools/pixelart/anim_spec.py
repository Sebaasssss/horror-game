"""Single source of truth for Codex-generated character animation.

Every strip is drawn on templates/template_<frames>.png. The game reads the same names from character.json,
so the frame list here, the Codex prompts (codex_jobs.py) and the importer (codex_char.py) stay in sync.
"""

CHARACTERS = {
    "jason": {
        "name": "JASON",
        "subtitle": "THE CAMP BUTCHER",
        "look": ("Jason Voorhees-style slasher: very tall, broad and hulking. Off-white hockey mask with red chevron marks "
                 "and breathing holes, bald scarred head behind the mask straps, worn torn dark olive-green work jacket "
                 "over a dark shirt, dark grey-brown trousers, heavy brown boots, black gloves. Long rusty bloodstained "
                 "machete in his RIGHT hand. Slow, heavy, unstoppable body language."),
        "weapon": "machete",
        "special": ("MACHETE RUSH: lowered-shoulder charge that ends in a lunging machete slash"),
    },
    "ghost": {
        "name": "GHOST",
        "subtitle": "THE MASKED STALKER",
        "look": ("Ghostface-style slasher: tall slim figure in a long flowing black hooded robe with ragged tattered "
                 "sleeves and a jagged tattered hem, white elongated ghost mask with black drooping eye holes and a long "
                 "open screaming mouth, black gloves, black boots. Large hunting knife with a silver blade in his RIGHT "
                 "hand. Agile, twitchy, theatrical body language; the robe flows with every move."),
        "weapon": "hunting knife",
        "special": "LUNGE STAB: a fast low dash forward ending with the knife thrust fully extended",
    },
}

# Limb-loss variants. Arms are lost free arm first, then the weapon arm; legs left first, then right.
VARIANTS = {
    "full": "",
    "one_arm": ("His LEFT arm (the free arm, the one without the weapon) has been cut off at the shoulder: a ragged "
                "bloody stump with exposed flesh and bone, dripping blood. He still holds the weapon in the right hand."),
    "no_arms": ("BOTH arms have been cut off at the shoulders: two ragged bloody stumps gushing blood, no weapon. "
                "He fights with headbutts and kicks."),
    "one_leg": ("His LEFT leg has been cut off at mid-thigh: a ragged bloody stump dripping blood. He balances and hops "
                "on his right leg. He still holds the weapon."),
    "no_legs": ("BOTH legs have been cut off at the hips: only the torso remains, dragging bloody guts behind, crawling "
                "on the floor with the arms and still gripping the weapon. The torso rests ON the green baseline."),
}

# name: (frames, frame-by-frame direction). {W} = weapon name.
FULL = {
    "idle": (6, "Fighting stance breathing loop, walk-in-place spot: 1 neutral guard, 2 shoulders rise slightly, "
                "3 top of the breath, 4 shoulders settle, 5 slight forward lean, 6 back to neutral (loops into 1)."),
    "walk_forward": (8, "Walking FORWARD toward the right, walk-in-place (do not drift inside the cell): "
                        "1 right foot forward heel contact, 2 weight down on the front leg, 3 left leg passing, "
                        "4 body up on the toes, 5 left foot forward heel contact, 6 weight down, 7 right leg passing, "
                        "8 body up. Torso bobs slightly, the free arm swings opposite, {W} kept ready in front."),
    "walk_back": (8, "Walking BACKWARDS: still FACING RIGHT toward the opponent but stepping back to the left, "
                     "walk-in-place, guarded posture with {W} held in front. Same 8 phases as a walk cycle in reverse: "
                     "rear foot reaches back, weight shifts back, front foot drags back, recover. Loop."),
    "crouch": (4, "1 knees starting to bend, 2 half crouch, 3 full low crouch guard with {W} ready, 4 same full crouch "
                  "(slight breathing variation)."),
    "jump_up": (4, "Vertical jump, body kept at the same height in the cell (the game moves it): 1 deep crouch preparing, "
                   "2 launching with legs extended, 3 knees tucked at the peak, 4 descending with legs reaching down."),
    "jump_forward": (6, "Forward somersault jump, body centred in the cell: 1 crouch takeoff leaning forward, "
                        "2 leaving the ground, 3 tucked ball rotating forward a quarter turn, 4 fully upside down "
                        "tucked, 5 rotating out of the flip, 6 legs extended ready to land."),
    "punch_1": (4, "Fast jab with the LEFT free fist (first hit of a combo): 1 guard chamber, 2 fist half way, "
                   "3 arm fully extended at head height, 4 retracting."),
    "punch_2": (4, "Follow-up straight cross with the right fist still gripping the {W} handle (second combo hit): "
                   "1 hips twisting, 2 extending, 3 full extension at chest height, 4 retracting."),
    "uppercut": (5, "Rising uppercut launcher (third combo hit): 1 dip low, 2 fist low near the hip, 3 driving upward, "
                    "4 fist high above the head with the body stretched up on the toes, 5 recover."),
    "slash": (6, "Heavy overhead {W} chop: 1 raising the {W} behind the head, 2 {W} fully back over the shoulder, "
                 "3 swinging down in front at head height, 4 impact low in front at waist height, "
                 "5 follow through near the floor, 6 recover to stance."),
    "slash_side": (5, "Wide horizontal {W} slash at chest height: 1 {W} pulled back behind the body, 2 starting the "
                      "swing, 3 {W} fully extended straight in front (arm straight), 4 follow-through across the body, "
                      "5 recover."),
    "kick": (5, "Front stomp kick: 1 knee up, 2 leg extending, 3 leg fully extended horizontally at stomach height "
                "with the sole forward, 4 retracting the knee, 5 foot down."),
    "roundhouse": (6, "Spinning roundhouse kick at head height: 1 pivot on the front foot, 2 turning the back toward "
                      "the viewer, 3 leg swinging around, 4 leg fully extended at head height, 5 leg coming down, "
                      "6 recover."),
    "sweep": (5, "Crouching leg sweep at ankle height: 1 drop low on one knee, 2 leg extending along the floor, "
                 "3 leg fully extended along the floor in front, 4 sweeping back, 5 crouch recover."),
    "crouch_attack": (4, "Crouching {W} thrust at shin height: 1 crouch, 2 thrusting, 3 {W} fully extended low in "
                         "front, 4 recover to crouch."),
    "air_attack": (4, "Airborne downward {W} strike, legs tucked: 1 {W} raised mid-air, 2 swinging down diagonally, "
                      "3 {W} fully extended downward in front, 4 held."),
    "air_kick": (4, "Flying jump kick: 1 tucked, 2 extending, 3 leg extended diagonally downward forward, 4 held."),
    "special": (6, "{SPECIAL}: 1 crouch leaning forward, 2 dashing low, 3 dashing with the body almost horizontal, "
                   "4 attack fully extended, 5 overextended follow-through, 6 recover."),
    "block": (4, "1 standing guard raising the forearms with the {W} crossed in front of the face, 2 standing guard "
                 "holding, 3 crouching guard, 4 crouching guard holding."),
    "hurt": (6, "1 head snapped back from a face hit, 2 recoiling further, 3 doubled over from a gut hit, "
                "4 doubled over deeper, 5 crouching hit flinch, 6 crouching hit flinch deeper. Blood flies from the hits."),
    "knockdown": (5, "Knocked down, the body stays inside the cell: 1 blasted off the feet flying backward to the LEFT "
                     "with the body tilted back, 2 airborne nearly horizontal, 3 falling back first, "
                     "4 lying flat on the back on the baseline, 5 lying flat (slight variation)."),
    "getup": (4, "1 lying propped on one elbow, 2 rolling onto one knee, 3 rising, 4 back in the fighting stance."),
    "grab_throw": (6, "Throw. Do NOT draw the opponent: 1 reaching forward with the free hand, 2 clutching with the fist "
                      "closed at chest height in front (holding someone by the throat), 3 lifting high overhead, "
                      "4 holding overhead, 5 hurling forward and down, 6 recover."),
    "thrown": (4, "Being thrown by an invisible opponent: 1 grabbed by the neck with the feet dangling, 2 lifted "
                  "overhead horizontally, 3 flying upside down, 4 slammed into the ground on the baseline."),
    "dizzy": (4, "Stunned and wobbling loop (the FINISH HIM state): arms limp, head lolling, knees weak, swaying."),
    "ko": (5, "1 staggering backward clutching the chest, 2 knees buckling, 3 dropping to the knees, "
              "4 falling face forward, 5 lying face down motionless in a pool of blood on the baseline."),
    "victory": (6, "Triumph sequence: slowly raising the {W} overhead, menacing pose, blood dripping from the {W}."),
    "intro": (4, "Round intro taunt: tilting the head, pointing the {W} at the opponent, menacing."),
    "headless": (5, "DECAPITATED body: NO HEAD, a ragged bloody neck stump spurting a tall fountain of blood: "
                    "1 standing rigid, 2 staggering, 3 knees buckling, 4 collapsing, 5 lying in a pool of blood."),
    "bisected": (5, "Body CUT IN HALF at the waist: 1 upper torso sliding off the legs with guts spilling, "
                    "2 upper half falling, 3 legs still standing gushing blood, 4 legs collapsing, "
                    "5 both halves on the ground in a pool of blood (draw both halves in the cell)."),
}

GORE = {
    "gore_parts": (8, "Separate dismembered body parts of this character, one per cell, centred, no full body: "
                      "1 severed head with mask and bloody neck, 2 ripped upper torso with exposed ribs, "
                      "3 severed LEFT arm, 4 severed RIGHT arm still gripping the {W}, 5 severed LEFT leg, "
                      "6 severed RIGHT leg, 7 pile of intestines, 8 bloody spine with pelvis. Every cut is ragged "
                      "and bloody with exposed bone."),
}

ONE_ARM = {
    "idle": FULL["idle"], "walk_forward": FULL["walk_forward"], "walk_back": FULL["walk_back"],
    "crouch": FULL["crouch"], "jump_up": FULL["jump_up"],
    "punch_1": (4, "Quick one-handed {W} jab: 1 chamber, 2 thrusting, 3 fully extended at head height, 4 retract."),
    "slash": FULL["slash"], "kick": FULL["kick"],
    "block": (4, "1 standing guard with the {W} raised in front of the face, 2 holding, 3 crouching guard, 4 holding."),
    "hurt": FULL["hurt"], "knockdown": FULL["knockdown"], "getup": FULL["getup"], "dizzy": FULL["dizzy"],
    "ko": FULL["ko"],
}

NO_ARMS = {
    "idle": (6, "Swaying menacing stance without arms, stumps gushing blood: breathing loop like a fighting stance."),
    "walk_forward": (8, "Walking forward without arms, walk-in-place, stumps dripping: full 8-phase walk cycle."),
    "walk_back": (8, "Walking backwards while FACING RIGHT, walk-in-place, stumps dripping: 8-phase cycle."),
    "crouch": FULL["crouch"], "jump_up": FULL["jump_up"],
    "headbutt": (5, "Brutal headbutt: 1 rearing the head back, 2 lunging forward, 3 head fully forward (impact), "
                    "4 recoil, 5 recover."),
    "kick": FULL["kick"], "sweep": FULL["sweep"],
    "hurt": FULL["hurt"], "knockdown": FULL["knockdown"], "getup": (4, "Getting up without arms: 1 lying, "
                                                                         "2 rolling to the knees, 3 rising, 4 standing."),
    "dizzy": FULL["dizzy"], "ko": FULL["ko"],
}

ONE_LEG = {
    "idle": (6, "Balancing on one leg loop, small hops in place, stump dripping blood, {W} ready."),
    "hop_forward": (6, "Hopping FORWARD on the right leg, hop-in-place: 1 crouch, 2 push off, 3 airborne, "
                       "4 peak, 5 landing, 6 absorb. The stump swings and drips blood."),
    "hop_back": (6, "Hopping BACKWARDS on the right leg while facing right, hop-in-place, same 6 phases."),
    "crouch": (4, "Kneeling down on the right knee, stump on the floor: 1 bending, 2 half, 3 kneeling guard, 4 hold."),
    "jump_up": (4, "One-legged jump: 1 crouch on one leg, 2 launching, 3 airborne, 4 descending."),
    "punch_1": FULL["punch_1"], "slash": FULL["slash"],
    "hurt": FULL["hurt"], "knockdown": FULL["knockdown"],
    "getup": (4, "Getting up on one leg: 1 lying, 2 on one knee, 3 rising leaning on the {W}, 4 balancing."),
    "dizzy": FULL["dizzy"], "ko": FULL["ko"],
}

NO_LEGS = {
    "idle": (4, "Torso propped up on both arms on the floor, breathing, guts trailing, {W} in the right hand."),
    "crawl_forward": (6, "Crawling FORWARD on the arms dragging the torso and guts, crawl-in-place: alternate arm "
                         "reaches, pulling the body, 6-phase loop."),
    "crawl_back": (6, "Crawling BACKWARDS while facing right, crawl-in-place, 6-phase loop."),
    "slash_low": (5, "Low {W} swipe at ankle height from the floor: 1 wind up, 2 swing, 3 fully extended, "
                     "4 follow through, 5 recover."),
    "lunge": (4, "Lunging forward from the floor with the {W} thrust: 1 coil, 2 launch, 3 fully extended, 4 land."),
    "hurt": (4, "Torso on the floor recoiling from hits: 1 head snaps back, 2 recoil, 3 flinch, 4 recover."),
    "dizzy": (4, "Torso on the floor wobbling, about to collapse, loop."),
    "ko": (4, "Torso collapsing face down in a pool of blood: 1 sagging, 2 falling, 3 down, 4 motionless."),
}

SETS = {
    "full": FULL,
    "one_arm": ONE_ARM,
    "no_arms": NO_ARMS,
    "one_leg": ONE_LEG,
    "no_legs": NO_LEGS,
}

# Animation names the game expects; each variant maps them onto the strips it has.
ALIASES = {
    "one_arm": {"punch_2": "punch_1", "uppercut": "slash", "slash_side": "slash", "roundhouse": "kick",
                "sweep": "kick", "crouch_attack": "punch_1", "air_attack": "slash", "air_kick": "kick",
                "special": "slash", "jump_forward": "jump_up"},
    "no_arms": {"punch_1": "headbutt", "punch_2": "headbutt", "uppercut": "headbutt", "slash": "headbutt",
                "slash_side": "headbutt", "roundhouse": "kick", "crouch_attack": "sweep", "air_attack": "kick",
                "air_kick": "kick", "special": "headbutt", "block": "crouch", "jump_forward": "jump_up"},
    "one_leg": {"walk_forward": "hop_forward", "walk_back": "hop_back", "punch_2": "punch_1", "uppercut": "slash",
                "slash_side": "slash", "kick": "slash", "roundhouse": "slash", "sweep": "punch_1",
                "crouch_attack": "punch_1", "air_attack": "slash", "air_kick": "slash", "special": "slash",
                "block": "crouch", "jump_forward": "jump_up"},
    "no_legs": {"walk_forward": "crawl_forward", "walk_back": "crawl_back", "crouch": "idle", "jump_up": "idle",
                "jump_forward": "idle", "punch_1": "slash_low", "punch_2": "slash_low", "uppercut": "lunge",
                "slash": "lunge", "slash_side": "slash_low", "kick": "slash_low", "roundhouse": "slash_low",
                "sweep": "slash_low", "crouch_attack": "slash_low", "air_attack": "lunge", "air_kick": "lunge",
                "special": "lunge", "block": "idle", "knockdown": "hurt", "getup": "idle"},
}

STAGES = {
    "camp": ("CAMP BLOOD", "Camp Crystal Lake at night: black pine forest, a misty lake with the full moon reflected, "
                           "wooden summer-camp cabins with one lit window, a wooden dock with a bloody canoe, "
                           "a hanging camp sign, fog, fireflies, a bloody axe stuck in a stump."),
    "utez": ("UTEZ", "The entrance gate of a Mexican technological university (Universidad Tecnologica Emiliano "
                     "Zapata, UTEZ) turned into a horror scene at night: tall terracotta-red cylindrical concrete "
                     "pillars, a long cream concrete beam across the top with the sign 'UNIVERSIDAD TECNOLOGICA "
                     "EMILIANO ZAPATA', red metal bar gates half open and bent, a round red guard booth with the "
                     "'UTEZ' logo and one flickering lamp, palm trees and wilted bougainvillea, blood smeared on the "
                     "pillars, a broken street light, fog, full moon, abandoned and ominous."),
    "woodsboro": ("WOODSBORO", "A quiet American suburban street at night on Halloween: a two-storey wooden house "
                               "with a porch and lit windows, a phone ringing inside, jack-o-lanterns on the steps, "
                               "police tape, a swing hanging from an old oak tree, picket fence, parked car with "
                               "headlights on, fallen leaves, fog and a full moon."),
}
