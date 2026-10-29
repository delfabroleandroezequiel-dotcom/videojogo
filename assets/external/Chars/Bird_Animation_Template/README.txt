BIRD ANIMATION TEMPLATE | VERSION 1.0

64x64 side-view pixel art facing right. Includes a color-coded reusable
template and finished crow and sparrow examples. Each contains 12 clips
and 96 editable frame slots: 36 clips and 288 frame slots across the pack.

START HERE
Preview/Complete_Showcase.gif shows Template, Crow and Sparrow left to right.
Preview/Animation_Overview.png shows the approved action stages.
Each bird folder contains one layered Aseprite source, a 6144x64 horizontal
atlas, Aseprite atlas JSON, individual 64x64 PNGs, palette and Animations.json.
Choose the combined atlas OR individual PNGs for your engine workflow.

EDITING
Open Template/Template.aseprite to recolor or adapt the template. Sources
contain six art layers: Wing Far, Tail, Feet, Body, Head, Wing Near.
The template and sparrow have hidden shoulder guides; keep these hidden
when exporting final artwork. Finished examples are in Crow and Sparrow.
Separate action sources and per-layer PNGs are unnecessary: the tagged,
layered combined source contains every approved action.
Color roles: body green, head light blue, near wing orange, far wing bright
cyan, tail purple, feet coral red. The template covers one feathered bird
body plan; larger anatomy changes need pose editing.

ANIMATIONS AND SOURCE FRAME RANGES (1-BASED)
Takeoff             1-12    930 ms
Flight            13-20    640 ms, loops
Landing           21-32    920 ms
Perched flick     33-44    650 ms
Glide             45-52   1080 ms
Glide to landing  53-56    300 ms
Hop               57-64    750 ms
Walk              65-72    640 ms, loops
Standing to walk  73-76    320 ms
Walk to standing  77-80    420 ms
Peck              81-88    710 ms
Look around       89-96   2100 ms

IMPORT AND PLAYBACK
Use nearest-neighbor filtering, no smoothing, and integer scaling.
Atlas cells are 64x64, with no spacing, margins, rotation or trimming.
Animations.json supplies per-frame milliseconds, loop/hold behavior,
atlas rectangles, source tags and transition schedules. Do not replace
unequal frame holds with a single FPS. Source tags and sequence frame
numbers are 1-based; atlas coordinates and Aseprite atlas JSON are 0-based.
Use the full 64x64 cells with a consistent origin; do not center each pose
by its visible bounding box. Grounded standing feet reach native row 59.

TRANSITIONS
Takeoff includes flight frame 1 and its hold; continue flight at frame 2.
Enter glide after flight frame 2; return to flight at frame 4.
For landing from glide: glide frames 1-5, glide-to-landing frames 1-4,
then landing frames 1-12. This route lasts 2020 ms and holds standing.
Standing-to-walk supplies walk frame 3 and its hold; continue at frame 4.
Branch to walk-to-standing when walk frame 1 is due. Avoid repeating
the shared anchor hold. Other gait phases need a wait or custom connector.
Walk is in place: 2 native pixels per pose, 16 per cycle, 25 pixels/second.
Use the root-motion metadata for the start/stop transitions. Sparrow walk
is stylized; hop is also included. Standing actions share standing endpoints.

PREVIEW
The complete showcase GIF repeats for viewing and includes longer resting
pauses. Its 15260 ms presentation is not the runtime clip timing. Use each
bird's Animations.json for runtime timing and source-only playback schedules.
No preening animation is included.

COMPATIBILITY
PNG exports and JSON are engine-neutral assets, not ready-made engine
controllers. Game-engine import and playback testing has not been performed.
