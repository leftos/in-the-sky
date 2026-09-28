# Godot client conventions

These are the rules a Godot .NET client breaks most often, seeded from the user-level rulebook. The implementer reads this document before any edit under `src/Sky.Client`, and `godot-reviewer` checks a change against it, naming the rule's heading in a finding. The client draws Session views (`ISkySession`) and holds no rules; where a rule below says "the rules engine", read `Sky.Engine` behind `Sky.Session`. The `godot-conventions-sync` skill keeps this file and the rulebook in step; `Seen:` counts the findings raised here.

## Views and commands

### Ask the rules engine what is legal; compute no rule in the client
<!-- rule: client-computes-no-rule -->

The client renders the view it was handed and enables a control only because the rules engine's legality answer said so, for the view on screen now. It never re-derives a rule from state (whose turn it is, what something costs now, which targets a choice may take, who still owes an answer), never retypes a rules constant, and never invents a fact the content catalogue holds. When the engine has no answer for the question, the fix is an engine change raised in the plan, not a predicate in a screen; until it lands, drop the number rather than show one the client worked out.
Source: the project's dumb-renderer rule (the engine decides; the client renders the view and submits a choice). Seen: 0 here (seeded from delve-the-dungeon).

### Keep `Bind` idempotent, and never submit from it
<!-- rule: bind-idempotent-no-submit -->

`Bind` with the same view changes nothing: it compares what it has against what it is given and touches only the controls whose state changed. Freeing and re-creating a row, a button or a dropdown on every update destroys the focus, hover or open popup the player had on it, and overriding every header per bind fires a theme notification and a re-sort for each. Records carrying `ImmutableArray` compare by reference, so compare their fields. A `Bind` never sends a command, and a pick a player may decline is never auto-answered because one choice is left. A side effect a bind may have that is not a command (writing a "seen" mark to the player's record, or telling a server about it) sits behind a flag the same pass has just set, so the second bind of one view does nothing; and when the side effect is a message to a server, the server answers to everybody only when what it was told changed what it holds, so a send that tells it nothing new ends there. The second condition is what bounds the exchange, not where the data came from.
Source: the project's architecture doc; `F:\Godot\docs\class-ref-xml\doc\classes\Node.xml` (`queue_free`), `Object.xml` (`emit_signal` is synchronous). Seen: 0 here (seeded from delve-the-dungeon).

### Decide from the view you hold now, not one captured earlier
<!-- rule: decide-from-current-view -->

A choice captured when a picker opened, a target chosen when an item was lifted, a trust verdict derived at fetch time, a refusal keyed on an event index that two views can share: each is acted on later against a different view. Re-read the option from the current view at submit time, carry the verdict with the signal that announces it, and key per-view memory on a client-side counter that advances on every game update, not on a position in the event stream, which two views can share. A field that mirrors a control (an open offer, a target prompt, a screenshot buffer) is cleared in the same call that hides the control. Memory that is per stream rather than per view (which events have been logged) is the exception: it keys on the stream position and lives on the owner of the connection or the game, not on a screen the router frees, and resets when the game does.
Source: the project's protocol or architecture doc (an event index is a position in a stream, not a view). Seen: 0 here (seeded from delve-the-dungeon).

### One press, one command
<!-- rule: one-press-one-command -->

Every path to a submit or a send sits behind an in-flight flag, a once-only flag or a control disabled until the next bind, including a prompt that can be re-shown before the acknowledgement or the re-bind, a button a second event could re-arm while its work is still running, a control a second click can reach while its animation runs, and a driver step that two callbacks can reach in one frame. A message the in-flight tracking cannot follow disables its button until the next update. Where the submit is synchronous, the flag guards the frames between the press and the bind that shows its result, not a round trip.
Source: the project's in-flight idiom. Seen: 0 here (seeded from delve-the-dungeon).

### Validate every string a manifest supplies before it reaches `OS` or the file system
<!-- rule: validate-manifest-strings -->

A URL that came from outside the build (an update manifest, a save, downloaded content) goes to `OS.ShellOpen` only with an `http` or `https` scheme; a file name taken from a URL keeps only `[A-Za-z0-9._-]` and the combined path is asserted to lie under the one `user://` folder meant for it; an installer or any other executable runs only from an `https` or loopback origin. The check lives where the outside data is parsed, so every consumer is covered.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\OS.xml` (`shell_open` opens bare paths too). Seen: 0 here (seeded from delve-the-dungeon).

## Input and focus

### Bind keys as InputMap actions, never to a key a `ui_*` action owns, and show the binding from the map
<!-- rule: keys-as-inputmap-actions -->

Keys are `project.godot` actions tested with `IsActionPressed`, never raw keycodes. Space is `ui_accept` and Escape is `ui_cancel`, so a focused button takes them before `_unhandled_input` does; pick a key no built-in action owns. Text or a tooltip that names the key reads it from `InputMap.ActionGetEvents` (a `Shortcut` is a snapshot of the map, and the comment says so), and a comment that claims a pad binding is checked against the live map.
Source: `F:\Godot\docs\manual\tutorials\inputs\input_examples.rst`; `F:\Godot\docs\class-ref-xml\doc\classes\BaseButton.xml` (`shortcut_in_tooltip`), `Node.xml` (`_unhandled_input` receives only what no control consumed). Seen: 0 here (seeded from delve-the-dungeon).

### Pick the input callback by the propagation order
<!-- rule: input-callback-by-propagation -->

`_input` runs before GUI input, `_shortcut_input` before `_unhandled_key_input`, and `_unhandled_input` last and for mouse motion too. Keys go in `_unhandled_key_input`, which receives `InputEventKey` alone, so an action a pad button can fire too (`ui_cancel` on B) goes in `_unhandled_input` and is tested with `IsActionPressed`; a visible button with a matching shortcut consumes the key first, so a fallback branch behind it is dead; an `_input` that swallows keys while a dialog is up starves the dialog's `LineEdit`. A flow that hides a modal, awaits, and shows a dialog sets a flag the input handler tests, so the key that opened the modal cannot re-open it mid-flow.
Source: `F:\Godot\docs\manual\tutorials\inputs\inputevent.rst`; `F:\Godot\docs\class-ref-xml\doc\classes\Node.xml` (`_shortcut_input`, `_unhandled_key_input`). Seen: 0 here (seeded from delve-the-dungeon).

### A modal traps focus, restores it on close, and a rebuild never drops it
<!-- rule: modal-traps-focus -->

Focus navigation is viewport-wide, so a modal sets `FocusBehaviorRecursive.Disabled` on the covered holder while open (or closes its own focus ring with `focus_next`/`focus_neighbor_*`) and sets `mouse_force_pass_scroll_events = false` on its root. The mouse is fenced the same way: `mouse_filter` is per control, so `MouseFilter.Ignore` on a root leaves every child under it clickable (and a full-rect root at the default `Stop` is the one blocker it has); anything taken out of play — a covered holder, a screen fading out — gets `MouseBehaviorRecursive.Disabled` beside its `FocusBehaviorRecursive.Disabled`. `process_mode` gates none of this: a disabled node still receives `_gui_input`. `Open()` caches `GetViewport().GuiGetFocusOwner()` before grabbing, `Close()` re-grabs it when still valid and visible, because hiding the focused control drops focus entirely. That fence is written once, in one modal shell scene under the components folder with an `Open(covered)`/`Shut(fallback)` pair: every dialog instances the shell and calls those two, and a dialog stands on its own full-rect node at the screen's root, never as a row of the screen's column, so opening it moves nothing behind it (a dialog placed as a row pushes the buttons under it below the bottom edge when its prompt is up). A modal that opens over another modal fences that modal, not the holder, since the lower shell already holds the holder, and tells the lower modal to stop reading keys while it stands, because an `_Input` beneath that swallows keys starves the upper dialog's field and one that answers the menu key tears itself down under it. A fence stops focus and the mouse, never a `Shortcut`: a hotkey on a covered screen still fires from `_shortcut_input`, so a modal's `_Input` swallows the covered screen's hotkeys itself, or its `_ShortcutInput` when the modal has a field to feed (`_Input` runs before the GUI and would starve the `LineEdit` of its letters, `_ShortcutInput` runs after it and still ahead of every `Shortcut` and `_UnhandledKeyInput` behind the modal, since the dialog is the last child of the screen and events go out in reverse depth-first order); a `shortcut_context` is no answer on a screen where nothing holds focus by default. A control the player is focused on or has open is updated in place, not freed and re-created. A row that is rebuilt on every bind records a key per control it builds (the item's identity, a constant for a lone toggle), reads the focus owner before it empties, and re-grabs the control carrying the same key after it fills; a persistent button is never hidden and re-shown inside one bind (assign `Visible` once per fill, since hiding the focused control drops focus), and only when nothing held focus, or what held it is gone, does the bind pick the next press. A modal that lives inside the screen it covers sets `focus_behavior_recursive = Enabled` on its own root so it stays focusable under the holder it disables. That same override keeps it focusable under any fence, a screen router's fade included: a screen that owns such a dialog takes it down in a hook the router calls before it fences the leaving screen, while the screen is still live, and the hook never sends, never emits outside its screen and never grabs focus. A control that is meant to take the focus and whose class default is not `FOCUS_ALL` sets `focus_mode = 2` in the scene before anything grabs it: a `RichTextLabel` defaults to 3, `FOCUS_ACCESSIBILITY`, focusable only under a screen reader, so a `GrabFocus()` on one is a no-op and a ring closed through it is open.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Control.xml` (`find_next_valid_focus`, `focus_behavior_recursive`, `mouse_behavior_recursive`, `mouse_force_pass_scroll_events`, `FOCUS_ACCESSIBILITY`, "Control nodes lose focus … if you hide the node in focus"; `_gui_input`'s list of reasons an event is not received names no process mode), `RichTextLabel.xml` (`focus_mode` default 3), `Viewport.xml` (`gui_get_focus_owner`), `F:\Godot\docs\manual\tutorials\scripting\pausing_games.rst` (what a paused node stops receiving). Seen: 0 here (seeded from delve-the-dungeon).

### Turn `_Process` and `_Input` off when they have nothing to do
<!-- rule: disable-idle-process-input -->

Overriding `_Process` or `_Input` enables it for the node's whole life, in every shipped build. `SetProcess(false)` while there is nothing to poll or when the branch is driver-only, `SetProcessInput(false)` while a menu is closed, and turn them back on at the transition; the `if (!active) return;` guard is not a substitute.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Node.xml` (`_process`, `set_process`); `F:\Godot\docs\manual\tutorials\scripting\idle_and_physics_processing.rst`; `F:\Godot\docs\manual\tutorials\best_practices\godot_notifications.rst`. Seen: 0 here (seeded from delve-the-dungeon).

### Write a selection back and guard re-entry on `TabBar` and `OptionButton`
<!-- rule: selection-writeback-reentry-guard -->

After `ClearTabs` and `AddTab`, set `CurrentTab` explicitly; whether `add_tab` or `current_tab` emits `tab_changed` synchronously is not documented, so a handler that can fire inside `Bind` is guarded by a `rebuilding` flag and a `moved` bool captured before the assignment. An `OptionButton` without `allow_reselect` cannot re-fire on the same item, so after a pick that failed to send, `Select` the header item back.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\TabBar.xml` (`add_tab`, `current_tab`, `tab_changed`), `OptionButton.xml` (`allow_reselect`, `select`). Seen: 0 here (seeded from delve-the-dungeon).

### Show a disabled or focused state
<!-- rule: show-disabled-focused-state -->

A `flat` button that is disabled looks like one that is not; a variation that overrides `normal`, `hover` and `pressed` but not `focus` inherits a ring the fill can hide. Every variation of a button declares its focus style in a colour that contrasts with its fill, and a disabled node reads as disabled (`Modulate`, or drop `flat`).
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Button.xml` (`flat`, `focus`); `F:\Godot\docs\manual\tutorials\ui\gui_theme_type_variations.rst`. Seen: 0 here (seeded from delve-the-dungeon).

### A control laid over a moving card takes its hit where it is drawn, and hands a press to the card's own controls first
<!-- rule: hit-area-asks-the-cards-controls -->

A control that lifts, turns or restacks under the pointer (a card in a fanned hand) has its hit rect where it is drawn, not where the player thinks it is, so a row of such cards takes every hover and press on plain slots laid out at each card's resting rect and never moved. A slot then owns every click on the card, and a live control on the card's face (a check box, a small button) is dead unless the slot asks the card first: on a release, the card is asked whether the point falls inside one of its own visible, enabled controls and presses that control through itself, and only otherwise is the card pressed. The same holds for a control laid over a card's full-rect button that passes the mouse: it keeps the click from the button, so a label over a button ignores the mouse and the button carries the tooltip. Focus on any of the card's controls counts as the card being looked at.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Control.xml` (`mouse_filter`, `_gui_input`, `get_global_rect`); `F:\Godot\docs\manual\tutorials\ui\gui_using_theme_editor.rst` is silent on it, the rule is measured. Seen: 0 here (seeded from delve-the-dungeon).

### Listen only to the gamepads players choose
<!-- rule: listen-only-to-chosen-pads -->

A machine can hold pads nobody is playing with (flight panels, a second controller on the desk; a flight-sim player is likely to have both), so the client never treats every connected pad as input. A seat is claimed by a button press on a pad, never by an axis, and until then that pad's events are ignored. When a pad connects (again on reconnect, and on demand from settings) its axes are sampled while idle: each resting value becomes that axis's centre, and a pad whose axes jitter at rest is flagged as noisy (one device measured in another project rests a stick at -0.98 and jitters). A device `Input.is_joy_known` does not map as a gamepad is listed in settings but never offered at the claim prompt. An agent's injected pad sits on an id no real pad holds and claims its seat the same way, by a button press at the selection step.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Input.xml` (`is_joy_known`, `get_joy_axis`, `get_connected_joypads`, `joy_connection_changed`). Seen: 0 here (seeded from godot-mcp).

## Scenes and theme

### Write the scene file the editor would write
<!-- rule: scene-file-as-editor-writes -->

A `.tscn` stores only values that differ from the default and from the instanced scene's own root: no `mouse_filter = 0`, no `visible = false` equal to the instance, no `layout_mode` under a plain `Control`, no `unique_name_in_owner` on a node nothing looks up, no `SizeFlags.Fill` in code either. The editor deletes such lines on its next save, and the diff then looks like a change.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Control.xml` (property defaults). Seen: 0 here (seeded from delve-the-dungeon).

### A `Transform3D` in a scene file is written by rows; check where −Z and +Z point
<!-- rule: transform3d-rows-check-z -->

The nine basis numbers of a `Transform3D` in a `.tscn` are the matrix row by row, so a node's own axes are its columns: every third number. A rotation written from memory as its columns comes out transposed, which for a pure rotation is the opposite turn: a camera meant to look down looks up at nothing, a light meant to shine down shines up, a `Label3D` meant to face up faces the floor. Nothing catches it, since headless renders nothing and the scene still loads clean. Before a hand-written 3D transform is called done, read column 2 off it and say where it points: a `Camera3D` looks along −column 2, a `DirectionalLight3D` emits along −column 2, a `Label3D` or `Sprite3D` shows its front along +column 2 and its head along +column 1. A pitch of θ about X is `Transform3D(1, 0, 0, 0, cos θ, −sin θ, 0, sin θ, cos θ, …)`: looking 50° down is θ = −50°, `(1, 0, 0, 0, 0.642788, 0.766044, 0, −0.766044, 0.642788)`; straight down, θ = −90°, is `(1, 0, 0, 0, 0, 1, 0, −1, 0)`. A rotation the script also builds (`new Quaternion(Vector3.Right, −MathF.PI / 2f)`) must agree with the scene's by construction, one derived from the other's convention and the comment saying which.
Source: `F:\Godot\docs\manual\engine_details\file_formats\tscn.rst` (the editor-written `Camera3D` at (0, 1, 3) aimed at the origin: `Transform3D(1, 0, 0, 0, 0.939693, 0.34202, 0, -0.34202, 0.939693, 0, 1, 3)`), `DirectionalLight3D.xml` ("Light is emitted in the -Z direction of the node's global basis"), `SpriteBase3D.xml` (`axis`: the front of the texture faces +Z). Seen: 0 here (seeded from delve-the-dungeon).

### One owner per property: the scene or the script, never both
<!-- rule: one-owner-per-property -->

A size, a visibility, a `scroll_following`, a launch state is set in the `.tscn` or in the script, not in both. When code needs the value, read it from the node (`GetCombinedMinimumSize`) after instancing rather than keeping a constant that a scene edit silently detaches, and only after a layout pass: read inside a `Bind`, or on a hidden control holding autowrap labels, the number is noise (655, 1351 and 2041 px were measured on three successive binds of one dock, and 535 for the hidden dock against 308 once laid out), so a real minimum is read on the frame after the control is shown and filled. A control's state before its first `Bind` is authored in the scene, so the editor shows the launch state.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Control.xml` (`get_combined_minimum_size`), `RichTextLabel.xml` (`scroll_following`). Seen: 0 here (seeded from delve-the-dungeon).

### Colour, font and size come from the theme; a replacement variation is 1:1
<!-- rule: theme-owns-colour-font-size -->

No colour, font or font size lives in a script or as a `theme_override_*` in a scene; scenes pick `theme_type_variation` names from the one class that spells them, and code reads colours with `GetThemeColor` under the theme's own types. A local override beats the variation, so an override left beside one makes the variation dead. When a variation replaces an override, it reproduces font, size and colour exactly; a look change is its own step, named as such. The default font has one owner, the theme's `default_font`. A weight or any other axis of a variable font is a `FontVariation` whose `variation_opentype` key is the OpenType tag as the integer the editor writes (`2003265652` for `wght`); `FontVariation.xml` says a name is accepted too, but the name it means is the readable one the text server gives the axis, not the four-letter tag, so `"wght"` is accepted and silently ignored and the face draws regular (measured: a serif at 18 px, 172 px wide under `"wght": 700` and under no variation, 183 px under the tag).
Source: `F:\Godot\docs\class-ref-xml\doc\classes\FontVariation.xml` (`variation_opentype`), `Font.xml` (`get_supported_variation_list` answers by tag); `F:\Godot\docs\manual\tutorials\ui\gui_skinning.rst`; `F:\Godot\docs\manual\tutorials\ui\gui_theme_type_variations.rst`; `F:\Godot\docs\class-ref-xml\doc\classes\Control.xml` (theme lookup order). Seen: 0 here (seeded from delve-the-dungeon).

### Spell a name once
<!-- rule: spell-name-once -->

Theme names live in one static class and input actions in another, and both hold `StringName` fields, so a rename is one edit and an input handler converts nothing per event. Tween and signal targets use `PropertyName.Modulate` and `SignalName.Pressed`, never `"modulate"`. A label or a tooltip string that two screens share has one constant.
Source: `F:\Godot\docs\manual\tutorials\scripting\c_sharp\c_sharp_basics.rst` (`PropertyName`, `MethodName`, `SignalName`); `F:\Godot\docs\class-ref-xml\doc\classes\InputEvent.xml` (`is_action` takes a `StringName`). Seen: 0 here (seeded from delve-the-dungeon).

### Size containers and labels for the largest content the rules allow
<!-- rule: size-for-largest-content -->

A container's minimum is the larger of its own and its children's, so a column or row that must hold the most items the rules allow scrolls (`ScrollContainer`), fans or grids; it does not push the rest of the screen off the bottom. A label that may hold several long names sets `AutowrapMode` or `TextOverrunBehavior`; a control whose `Size` is set below its minimum grows anyway. After `SetItemText` on an `OptionButton`, the minimum size does not update at once. A width measured from text (`Font.GetStringSize`) is measured through a node that is in the tree, with the variation named as the theme type in `GetThemeFont`: a label that has just been built answers `GetThemeFont` with the default face at 16 px (measured: 69 px for a name then drawn at 105). Check the worst case with a screenshot, not the one-item case.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Control.xml` (`custom_minimum_size`, `get_theme_font`), `Label.xml` (`autowrap_mode`, `text_overrun_behavior`), `OptionButton.xml` (`fit_to_longest_item`). Seen: 0 here (seeded from delve-the-dungeon).

### A wrapping `fit_content` `RichTextLabel` is told its maximum width, and its bold size and leading
<!-- rule: richtextlabel-fit-content-max-width -->

A `RichTextLabel` that wraps and fits its own height measures that height against the width it had a layout pass ago, and it clips its content, so a height that comes out short cuts the last line off unseen. It gets a `custom_maximum_size` width: a number in the scene where the width is fixed, or, where the same scene stands in rows of different widths, the width of its own column read off the layout in a `Resized` handler and once when it is shown. A variation over `RichTextLabel` that sets `normal_font_size` sets `bold_font_size` beside it, since the two are separate items and a bold run otherwise takes the engine's fallback; and one that stands beside a `Label` of the same face sets `line_separation` to the `Label`'s `line_spacing` (3 by default against 0), or the two paragraphs read at different leadings. It also takes `focus_mode = 0` and `mouse_filter = 2` when it is only words on a button's face: the class defaults are accessibility focus and stop.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\RichTextLabel.xml` (`fit_content`, `autowrap_mode`, `focus_mode`, theme items `bold_font_size`, `line_separation`), `Label.xml` (`line_spacing`), `Control.xml` (`custom_maximum_size`). Seen: 0 here (seeded from delve-the-dungeon).

### Defer a scroll position until the container has sorted
<!-- rule: defer-scroll-until-sorted -->

`ScrollContainer` applies a scroll value in its own sort pass, so set it with `SetDeferred(PropertyName.ScrollVertical, value)`, or build the tween that moves it inside a method `CallDeferred` reaches, never inside a `SortChildren` handler, and call `QueueSort` yourself when a later `Bind` changes what to centre on without changing any minimum size. A minimum size that depends on the container's own size is set in the layout pass and re-arms the centring when it changes.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\ScrollContainer.xml` (`scroll_vertical`), `Container.xml` (`queue_sort`, `sort_children`). Seen: 0 here (seeded from delve-the-dungeon).

### A container owns its child's position, size and first scale; animate what it does not
<!-- rule: container-owns-child -->

A `Container` writes each child's `position` and `size` on every sort, and its `rotation` to 0 and `scale` to `(1, 1)` too (`Container::fit_child_in_rect`, `scene/gui/container.cpp:126-127` in the engine source; the class reference says only that the scale is reset at instancing), so a turn set once on a container child is gone at the next sort. A turn a container child must keep is written again on the container's `NotificationSortChildren`, after the engine's own fit. A tween on a container child's `Position` is overwritten by the next sort, and a `Scale` assigned in the frame the scene is instanced is gone before the tween reads it. Motion on a container child is `Scale` (or `Modulate`) with the start given to the tweener by `.From(...)`, never assigned first; a value derived from the layout (a `PivotOffset` at the bottom edge, a centre) is set from a `Resized` handler and once in `_Ready`, never from `Size` inside the call that starts the tween, because `Bind` runs before the sort that fills the row. For items that must travel (a fanned hand, a dragged piece) this means either they are not container children and their owner computes their positions, or their motion is limited to `Scale` and `Modulate`.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Control.xml` (`scale`: "If the Control node is a child of a Container node, the scale will be reset to Vector2(1, 1) when the scene is instantiated"), `Tween.xml` (`tween_property`: the start is "the property's value at the time the tweening … starts"), `PropertyTweener.xml` (`from`), `Container.xml` (`sort_children`), `scene/gui/container.cpp` (`fit_child_in_rect`). Seen: 0 here (seeded from delve-the-dungeon).

### Import downscaled art with mipmaps, and set no filter a `DPITexture` ignores
<!-- rule: mipmaps-and-dpitexture-import -->

Art drawn below its raster size needs `mipmaps/generate=true` and `TextureFilter.LinearWithMipmaps`, or it is grainy. A `DPITexture` re-rasterises to the viewport scale and has no mipmap chain, so `texture_filter` on its rect is noise; drop it, and check the result at 1080p rather than reasoning about it. Once a project's SVGs are `DPITexture`s, a new SVG's `.import` must read `importer="svg"` and `type="DPITexture"` like the rest, and the headless import does not write that on its own: Godot's default for an SVG is the raster importer (`importing_images.rst`, "By default, SVGs are rasterized at import-time"; `DPITexture` is the per-file opt-in from the Import dock), and `--import --quit` only fills in the `.import` a file already has. So a new SVG lands with a hand-written stub beside it before the import runs, the `[remap]` block reading `importer="svg"` and `type="DPITexture"`, the `[deps]` block naming its `source_file`, and the six `[params]` an SVG carries (`base_scale=1.0`, `saturation=1.0`, `color_map={}`, `fix_alpha_border=true`, `premult_alpha=false`, `compress=true`); the import then fills `uid`, `path` and `dest_files`. A set of glyphs that lands as `importer="texture"` is a frozen raster that a 1080p drive cannot tell from the real thing (it shows only in a window at another scale), so the art test for a folder of SVGs asserts the importer.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\CanvasItem.xml` (`texture_filter`), `ResourceImporterTexture.xml` (`mipmaps/generate`), `DPITexture.xml`. Seen: 0 here (seeded from delve-the-dungeon).

### Draw pixel art at its own resolution, scaled up in whole steps
<!-- rule: pixel-art-world -->

Pixel art is the opposite case to downscaled art: it is imported lossless with no mipmaps (`compress/mode=0`, `mipmaps/generate=false`), drawn at its native size inside a SubViewport of the world's resolution whose `canvas_item_default_texture_filter` is nearest (`0`), and that viewport is scaled up by a whole number (x3 for 640 by 360 on 1080p), with the window's `content_scale_stretch` on integer. A texture filter set on a node inside the world instead of the viewport default is one more place for linear to creep back in, so none is set there. Text in the world is a pixel font at its native size with antialiasing off.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Viewport.xml` (`canvas_item_default_texture_filter`), `Window.xml` (`content_scale_stretch`), `ResourceImporterTexture.xml`. Seen: 0 here (seeded from opening-hand).

### Author a procedural texture at the size it is drawn, filling all of it
<!-- rule: procedural-texture-drawn-size -->

A `GradientTexture2D` is generated on the CPU at `width` × `height`; a smooth radial needs 256 px and a `TextureRect` that scales it, not 1400 × 1400. `fill_from`/`fill_to` are UV coordinates, so a radius of 0.5 leaves half the texture transparent; set the rect's offsets to match the visible half or fill the whole.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\GradientTexture2D.xml`, `TextureRect.xml` (`stretch_mode`). Seen: 0 here (seeded from delve-the-dungeon).

### A 2D light lights by layer range, not by the canvas it sits on
<!-- rule: light2d-layer-range -->

`Light2D.range_layer_min`/`range_layer_max` filter the CanvasLayer *index of the item*, and both default to 0, so a light placed on a backdrop `CanvasLayer` at -1 lights the main canvas and leaves its own layer dark until the range names -1. Set the range from the layer the node is instanced under, in `_Ready`, rather than as a number in the scene that must match another scene's number; a `CanvasModulate` is scoped per canvas and never crosses a `CanvasLayer`. Measured in a scratch scene: range `[0, 0]` lit a `PanelContainer` on the main canvas from (38,43,53) to (64,66,71) and left the backdrop at (7,7,10); range `[-1, -1]` lit the backdrop to (47,42,38) and left the panel byte-identical.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Light2D.xml` (`range_layer_min`, `range_layer_max`), `CanvasLayer.xml` (the default scene renders at index 0), `CanvasModulate.xml` (one per canvas; CanvasLayers render independently). Seen: 0 here (seeded from delve-the-dungeon).

### A 2D light scales the item's own colour; energy cannot brighten a near-black rect
<!-- rule: light2d-scales-item-colour -->

`LIGHT = LIGHT_COLOR.rgb * COLOR.rgb * LIGHT_ENERGY`, so a `ColorRect` at 0.08 grey peaks near 0.28 under energy 3.6 and reads as a smudge; a pool of light is made by raising the ground's albedo or the `CanvasModulate`, not the light's energy, and the falloff bands under the Compatibility renderer's RGBA8 unless the ambient hides it (a project on a renderer with debanding avoids that, but a player whose machine falls back to Compatibility still sees the bands). A light reveals texture; it does not create brightness.
Source: `F:\Godot\docs\manual\tutorials\shaders\shader_reference\canvas_item_shader.rst` (the `light()` function), `F:\Godot\docs\manual\tutorials\2d\2d_lights_and_shadows.rst` ("the background color does not receive any lighting"), `F:\Godot\docs\manual\tutorials\rendering\renderers.rst` (debanding unsupported under Compatibility). Seen: 0 here (seeded from delve-the-dungeon).

### Export tunables and scenes; an `[Export]` on a script autoload is inert
<!-- rule: export-tunables-and-scenes -->

Screens and components are `[Export] PackedScene` fields assigned in the owning `.tscn`, not `GD.Load` of a string path at swap time, and so is a resource a scene hands on (a theme, a data resource the next screen is dressed in). A value that will be tuned by hand (a gap, a width, a hold, a timeout) is `[Export]`ed on the node the scene instances. An autoload registered as a script gets a bare `Node` created for it, so an `[Export]` there has no inspector; make such values constants or autoload a scene.
Source: `F:\Godot\docs\manual\tutorials\best_practices\godot_interfaces.rst`; `F:\Godot\docs\manual\tutorials\scripting\singletons_autoload.rst`; `F:\Godot\docs\manual\tutorials\scripting\c_sharp\c_sharp_exports.rst`. Seen: 0 here (seeded from delve-the-dungeon).

### A subtree used twice becomes a scene; a helper written three times becomes one method
<!-- rule: subtree-twice-becomes-scene -->

A backdrop, a tab bar, a status chip, a bordered panel, a `RemoveChild`+`QueueFree` loop: each gets copied into several scenes or scripts before it becomes a component scene or a node-extension method. Two copies of 40 lines is the threshold for a scene; the third copy of anything is the threshold for a method. Two wordings of one label drift; one wording class per kind of thing.
Source: `F:\Godot\docs\manual\tutorials\best_practices\scene_organization.rst` (a subtree instanced "without requiring details about their environment"). Seen: 0 here (seeded from delve-the-dungeon).

### Resolve a node once in a hot path
<!-- rule: resolve-node-once-hot-path -->

A `%Name` property re-runs `GetNode` per access; inside a rebuild loop, a per-frame fade, or a per-item `Bind`, resolve it into a field in `_Ready`. The converse holds too: a node read only in `_Ready` (a label written once) is a local there, not a field, so the fields a class keeps are the ones its hot path reads.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Node.xml` (`get_node`, store the reference). Seen: 0 here (seeded from delve-the-dungeon).

### What a script makes from a theme read, it makes again on `NotificationThemeChanged`
<!-- rule: remake-theme-reads-on-theme-changed -->

A script that reads a theme item and keeps what it made from it (a colour written into BBCode runs, a mark or a material; a height summed from the theme's constants; a width measured in the theme's face) overrides `_Notification` and makes it again on `NotificationThemeChanged`, or a theme swap at runtime leaves it standing in the old theme. The notification is sent when the theme changes on the node or any ancestor, when the node's variation or one of its own overrides changes, and when the node enters the tree; that last one arrives alongside `NotificationEnterTree`, before a scene's children are initialized, so a handler that touches a child or a cache built from one is guarded by `IsNodeReady()` and `_Ready` does the first write itself. Where there may be nothing to make again (no runs written yet, no marks standing), the guard says so too. A handler that only drops a flag (forget the cached BBCode, make it on the next opening) touches no node and carries no guard. Since a change to the node's own overrides sends the notification again, a handler writes overrides on its children, never on itself. The handler calls no `base._Notification`, as the manual's C# example calls none.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Control.xml` (`NOTIFICATION_THEME_CHANGED`: the four cases, the note on `is_node_ready`); `F:\Godot\docs\manual\tutorials\best_practices\godot_notifications.rst` (the C# `_Notification` override). Seen: 0 here (seeded from delve-the-dungeon).

### Read the window's mode off the window, and store nothing for its return
<!-- rule: window-mode-off-the-window -->

A saved window mode is applied once, in the main scene's `_Ready`, and after that the window is the truth: a toggle key and a settings row read `Window.Mode`, never the saved file, because a command-line switch (`--windowed`), a title-bar maximize or a restore leaves the file and the window apart, and a row that reads the file then shows the wrong mode, re-picking it fires nothing (`OptionButton` without `allow_reselect`) and the first toggle overwrites the player's choice. Borderless windowed is `Window.ModeEnum.Fullscreen`, a frameless window at the monitor's size with the video mode untouched (`Window.xml`, `mode`: "Fullscreen mode is not exclusive full screen on Windows and Linux"), and the docs say nothing about the return; measured on a 5120 × 1440 screen: a 1920 × 1080 window at (1600, 156) comes back to 1920 × 1080 at (1600, 156), so no size or position is stored. Applied in `_Ready`, the first frame is drawn 52 ms later, mid-resize (3840 × 1080 on the way to 5120 × 1440); moving the call to an autoload's `_Ready` runs earlier but the frame is still drawn after it, so the lever against a visible frame is `display/window/size/mode` in `project.godot`, not the call's place.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Window.xml` (`mode`), `DisplayServer.xml` (`WINDOW_MODE_FULLSCREEN`: "will change the window size to match the monitor's size"). Seen: 0 here (seeded from delve-the-dungeon).

### A partial theme on an ancestor re-declares every base variation of a type it touches, and sets no `default_font`
<!-- rule: partial-theme-redeclares-base-variations -->

A theme set on an ancestor to restyle a subtree (a per-screen theme over the base project theme) is searched first, and for every type it names: a control looks its item up under its variation, then the variation's base types, then its class names, at each `theme` from branch to root, and the earliest match wins. So a partial theme that defines `Label/font_color` answers every `Label` variation below it before the project theme's variation is ever read, and the variations lose their own colour. Where a partial theme touches a base type, it re-declares every variation of that type the base theme defines, and the items a variation sets differently (a pixel font, an outline) are copied in. A `default_font` on the partial theme answers every font lookup in the subtree that finds no `font` item first, so it replaces the base theme's fonts wholesale; a partial theme sets fonts per type instead. Measured: a partial theme's `Label` colour reached every label variation, and its default font the pixel labels, until both were re-declared.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Control.xml` (`theme_type_variation`: "If the theme item cannot be found using this type or its base types, lookup falls back on the class names"; "Theme items are looked for in the tree order, from branch to root … The earliest match against any type/class name is returned. The project-level Theme and the default Theme are checked last"; `get_theme_default_font`). Seen: 0 here (seeded from opening-hand).

### A parent that measures a child after a theme change defers the measurement
<!-- rule: defer-measure-after-theme-change -->

`NotificationThemeChanged` reaches a parent before its children, so a parent that re-measures a child in its handler (a hand shrinking its cards to fit, a row summing its children's minimum sizes) reads the child's size in the old theme: the child has not yet remade its own theme reads. The parent defers the measurement (`CallDeferred`, or a flag read on the next frame) so every child has taken the new theme first. A red proof needs a theme whose change moves the measured size; with none, the immediate measurement stays green and the order is proven only by reading. Measured: a hand's shrink after a theme swap measured the old card faces until deferred.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Control.xml` (`NOTIFICATION_THEME_CHANGED`: sent when the theme changes on the node or any ancestor); the parent-first order measured at runtime. Seen: 0 here (seeded from opening-hand).

### Reset a control's size a frame after an autowrapping label joins it
<!-- rule: reset-size-after-wrap-layout -->

`ResetSize()` clamps a control to `GetCombinedMinimumSize()` as computed at that instant. An autowrapping `Label` just added to a container reports its minimum at its unlaid width (a few pixels, one letter a line) until the container's queued sort lays it at the column's width, so a reset in the same frame clamps the control to a height it keeps: a control never shrinks on its own when its minimum falls back. Defer the reset (`Callable.From(...).CallDeferred()`, guarded for liveness) to after the sort, or size the new label to the column's width before `AddChild`. Measured in another project: a card rebound twice in one frame grew from 192 to 296 px after its label was rewritten. A screenshot showing a "stray" or stretched node is checked against `GetCombinedMinimumSize()` before it is read as an extra node.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Control.xml` (`reset_size`, `get_combined_minimum_size`), `Label.xml` (`autowrap_mode`). Seen: 0 here (seeded from opening-hand).

## Async and lifetime

### Godot API on the main thread, CPU and IO off it
<!-- rule: godot-api-main-thread -->

Every node, `OS`, `ProjectSettings` and `Engine` call runs on the main thread: an `await` in a Godot callback resumes on `GodotSynchronizationContext` unless `ConfigureAwait(false)` moves it, so the outer awaits in a screen omit it and only the inner IO loop uses it. Hashing a large file, encoding a PNG or zipping a log goes through `Task.Run`; off-thread code reaches a node only through `CallDeferred` or a `Progress<T>` built on the main thread. A flag written off-thread and read on it uses `Interlocked` or is cleared in the deferred callable.
Source: `F:\Godot\docs\manual\tutorials\performance\thread_safe_apis.rst`; `F:\Godot\GodotSharp\Api\Debug\GodotSharp.xml` (`GodotSynchronizationContext`). Seen: 0 here (seeded from delve-the-dungeon).

### Check liveness after every `await` and inside every deferred lambda
<!-- rule: liveness-after-await -->

Between an `await` and its continuation the screen can be swapped and freed; a lambda passed to `CallDeferred` that captures `this` runs on a disposed node. Before touching a node after either, test `IsInstanceValid(this) && IsInsideTree()`, on the failure path as well as the success path, and log and return otherwise. The two exits of one method agree on what they check.
Source: `F:\Godot\docs\manual\tutorials\scripting\c_sharp\c_sharp_differences.rst` (`IsInstanceValid`); `F:\Godot\docs\manual\tutorials\scripting\c_sharp\c_sharp_signals.rst` (lambdas and freed receivers). Seen: 0 here (seeded from delve-the-dungeon).

### Cancel awaited work from `_ExitTree`
<!-- rule: cancel-awaited-work-exit-tree -->

A connect, a download, a save, an off-thread encode: each takes a `CancellationToken` a `CancellationTokenSource` on the owner cancels in `_ExitTree` or when the owner leaves, and the continuation checks the token again before its side effect (installing a socket, launching a process, writing the file, swapping the screen, quitting). Cancel the source; do not dispose it while a task still holds its token. An operation with no wall-clock bound has a stall bound instead.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\OS.xml` (`create_process` outlives Godot). Seen: 0 here (seeded from delve-the-dungeon).

### Observe every task, and set UI state before a call that can throw
<!-- rule: observe-every-task -->

`_ = SomethingAsync()` has a `catch (Exception)` with the CA1031 justification the sibling call sites already carry, or it is awaited. A signal handler that reads the registry or the file system does so after the row is visible and the label written, or inside a catch that logs and degrades, so a throw cannot leave the player with nothing. A cleanup call that can throw sits inside the `try` whose handler maps failures to player text.
Source: global `CLAUDE.md` (never swallow, never early-return without logging). Seen: 0 here (seeded from delve-the-dungeon).

### Keep a tween handle; kill it before a restart, never before its final frame
<!-- rule: tween-handle-kill-before-restart -->

A node that flashes keeps `private Tween? flash`; `Flash` kills the old one and creates a new one, so a skipped batch of events does not stack thirty tweens on `Modulate`. `Bind` kills the tween only when the state it ends on changes, because the last line of a batch and the `Bind` that follows land in the same frame and a kill at t=0 snaps the flash back before it is drawn.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Tween.xml` (initial value, `kill`, bound to the node). Seen: 0 here (seeded from delve-the-dungeon).

### A hold is an interval, never a wait on a GPU signal without a timeout
<!-- rule: hold-is-interval -->

An effect's length on screen is a `TweenInterval` the owner keeps a handle to, never an `await` on `AnimatedSprite2D.animation_finished`, `GpuParticles2D.finished` or a render-frame signal: a headless run renders neither, so a screen waiting on one waits for ever, and a released build with a stalled GPU does the same. Where a GPU signal is the only source of truth, the wait carries `SetTimeout` and the timeout path writes the same end state the signal would. The hold on a beat of the event log is the longer of its own reading time and the longest effect in the beat, so an effect is never cut off by the next beat and a short one never slows play.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Tween.xml` (`tween_interval`), `GPUParticles2D.xml` (`finished` is emitted only when `one_shot` and the particles are processed), `AnimatedSprite2D.xml` (`animation_finished`). Seen: 0 here (seeded from delve-the-dungeon).

### A tween built from data is checked for tweeners before anything waits on it
<!-- rule: check-tween-has-tweeners -->

A reveal that adds a tweener per beat can add none, and an empty tween errors on its first step and never emits `finished`, so whatever waits on that signal (a `done` callback, a router hold) waits forever. After building one, `if (!tween.HasTweeners())` kill it and settle as if it had finished.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Tween.xml` (`has_tweeners`: "Killing an empty tween before it starts will prevent errors"). Seen: 0 here (seeded from delve-the-dungeon).

### A deferred call made from a deferred call runs in the same idle cycle
<!-- rule: deferred-call-same-idle-cycle -->

`CallDeferred` does not buy a frame: idle time runs deferred calls until there are none left, so a method deferred from itself, or from anything it calls (a driver step, a signal handler it reaches), recurses inside one flush and freezes the client instead of looping visibly. A signal that re-enters the router is raised only when something actually changed (the emit sits behind the flag that says work was in progress), the deferral is narrowed to the state that is waiting on it, and the comment beside the emit names the termination condition rather than claiming the signal is raised unconditionally.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Object.xml` (`call_deferred`: "you should not call a method deferred from itself (or from a method called by it), as this causes infinite recursion the same way as if you had called the method directly"). Seen: 0 here (seeded from delve-the-dungeon).

### Process-wide services live in the autoload, added and removed symmetrically
<!-- rule: process-wide-services-in-autoload -->

The logger and the crash handler cover the whole process, so the autoload registers them beside the client log's open in its `_Ready` and removes them just before the log's dispose in its `_ExitTree`; a scene that registers one misses boot errors and, because siblings exit in reverse order, teardown errors too. Every `Add` has its `Remove`.
Source: `F:\Godot\docs\manual\tutorials\best_practices\autoloads_versus_regular_nodes.rst`; `F:\Godot\docs\manual\tutorials\scripting\logging.rst`; `F:\Godot\docs\class-ref-xml\doc\classes\OS.xml` (`remove_logger`). Seen: 0 here (seeded from delve-the-dungeon).

### Emit nothing during teardown
<!-- rule: emit-nothing-during-teardown -->

`_ExitTree` closes sockets and files and stops timers directly; it does not go through the state transition that emits a changed signal, because a listener may instantiate a screen into a tree that is shutting down. The exit order of siblings under root is reversed and otherwise undocumented; the transition skips `EmitSignal` when `!IsInsideTree()`.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Node.xml` (`_exit_tree`, NOTIFICATION_EXIT_TREE). Seen: 0 here (seeded from delve-the-dungeon).

### A logger callback never throws
<!-- rule: logger-callback-never-throws -->

`_LogError` runs on any thread and must not raise; it uses a null-returning accessor for the open log rather than one that throws, and wraps its `WriteLine` in the same `IOException`/`UnauthorizedAccessException` catches the file opener uses.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Logger.xml` (`_log_error`). Seen: 0 here (seeded from delve-the-dungeon).

### Delete a download or temp file on every failure path
<!-- rule: delete-partial-file-on-failure -->

A partial or unverified file under `user://` is removed in a `finally` (after the stream is closed), on cancellation, on a hash mismatch, on a throw from the verification or from the step that hands the file on; only a file that was handed over complete survives. The delete itself can throw, so it is logged, not allowed to replace the message the player was meant to see.
Source: none in the cache. Seen: 0 here (seeded from delve-the-dungeon).

### A tween paused from its last tweener finishes all the same
<!-- rule: tween-pause-needs-tweener-behind -->

A `TweenCallback` may pause its own tween and something else may `Play()` it later, but only while another tweener stands behind the callback: paused from its last tweener the tween raises `finished` in that very step and goes invalid, so whatever waited on `finished` moves on at once and the later `Play()` is an engine error (`Tween invalid. Either finished or created outside scene tree.`). A wait built this way ends on a short real interval after the callback (0.05 s is enough), never on the callback itself and never on an interval of no length, which a tween errors on. The wait still carries its ceiling, as any wait on a signal does.
Source: measured on Godot 4.7.2 with a two-tween probe (paused from a middle callback: `finished` false, valid, resumed by `play`; paused from the last: `finished` true, invalid); `F:\Godot\docs\class-ref-xml\doc\classes\Tween.xml` (`pause`, `play`) states neither case. Seen: 0 here (seeded from delve-the-dungeon).

## Text a player reads

### A refusal, a failed send or an error return is never silent
<!-- rule: refusal-never-silent -->

A submit, a send and `OS.ShellOpen` return a result; every caller reads it. A false, a refused choice or an `Error` writes a client log line and, where a player did something, a notice on the screen they are on, and the control returns to the state before the press (button enabled, dropdown back on its header, a lifted item back in its row). A refusal is routed to whichever screen is up, not only to the main game screen. A driver that could not send or press logs why. A message no press asked for and that goes again on its own (a keep-alive on its timer, a record re-sent whole on the next connect) is the exception: the send's own log line is the whole report, there is no control to put back, and the call site carries a comment naming what re-sends it.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\BaseButton.xml` (`disabled`), `OS.xml` (`shell_open`); global `CLAUDE.md`. Seen: 0 here (seeded from delve-the-dungeon).

### Text matches the screen and the state, one wording per thing, no raw exception text
<!-- rule: text-matches-screen-and-state -->

A label says what is true now: not "Everybody has voted." when nobody will, not "of this run" on a screen with no run, not "the left door" where there is one way. One thing has one name in every screen, through one wording class per kind of thing. An `Exception.Message` goes to the log; the player gets a sentence naming the operation and the host or file. An empty value renders a fallback word, not a dangling `v`, or nothing at all when the control has nothing to say (an empty target line is hidden, so the question beside it takes the row). Two controls that must agree about one value (an item's chips and the tooltip that explains them) are fed from one local, never from two copies of the expression, and whatever hides one hides the other.
Source: none in the cache; `F:\Godot\docs\class-ref-xml\doc\classes\OptionButton.xml` (`allow_reselect`) for the picker case. Seen: 0 here (seeded from delve-the-dungeon).

### A sentence names the thing; a card's title never stands inside it
<!-- rule: sentence-names-the-thing -->

A title is a label (`Learn a skill`, `Upgrade an ability`, `Relic: Gauntlet`); a sentence that reports what somebody holds, took or waits on names the thing as a noun (`the skill`, `the upgrade`, `Gauntlet`), through a wording of its own beside the title's, never by composing the title in. Every kind the sentence can name is covered by a fact, not only the kinds whose titles happen to read as nouns (`+1 XP`, `Draw two`): leaving the rest to the title is how the gap survives a review.
Source: none in the cache. Seen: 0 here (seeded from delve-the-dungeon).

### A waiting state always has a way out on screen
<!-- rule: waiting-state-has-way-out -->

When the screen waits on somebody else (other players, an AI's turn), the player sees that the wait is on and, whenever the rules would accept it or the wait has stopped advancing, a control that closes it (the host's override, a stall bound that logs and offers the menu), and never a message claiming the wait is over while the game still waits or while a command the rules refuse is being retried. A state the client cannot leave alone is raised as a server or rules-engine gate, not papered over.
Source: none in the cache. Seen: 0 here (seeded from delve-the-dungeon).

## Scripted drivers and smokes

### Press the control a player presses, or say the driver does not
<!-- rule: driver-presses-player-control -->

A driver step presses through the button (`Press()`, the picker's first choice) so `disabled`, in-flight and focus are exercised, and its summary says which. Where it raises `BaseButton.SignalName.Pressed` instead, the comment says that and the handler re-asks the rules. A step reports an action only after confirming it went (the in-flight flag went true, or the engine's pending decision moved on).
Source: `F:\Godot\docs\class-ref-xml\doc\classes\Object.xml` (`emit_signal`), `BaseButton.xml` (`disabled`, `action_mode`). Seen: 0 here (seeded from delve-the-dungeon).

### Reset the watchdog on real progress and key it on every field that moves
<!-- rule: watchdog-resets-on-progress -->

The stall clock starts at the first game update, not at launch, so the menu, the connect and the setup are not charged to it; its progress key includes every field a phase advances through (a step inside a phase, the active seat, a prompt's stage), not only the phase type and event count. Every step has a timed deadline of the same shape; none quits on the very next frame.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\SceneTree.xml` (`quit`, `process_frame`). Seen: 0 here (seeded from delve-the-dungeon).

### A driver log line prints once and names the cause when stuck
<!-- rule: driver-log-once-names-cause -->

`Quit()` takes effect at the end of the iteration, so a terminal line and its `Quit` sit behind a once-only bool; a milestone line remembers the update it reported. A driver that cannot proceed (nothing for it to answer, the wrong screen up) prints which, so the harness log names the failure rather than a 60-second silence.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\SceneTree.xml` (`quit`). Seen: 0 here (seeded from delve-the-dungeon).

### Exit non-zero on a failure path
<!-- rule: exit-nonzero-on-failure -->

`GetTree().Quit()` defaults to 0, so a stall or a run that ended before the smoke's goal calls `Quit(1)`; the process signal agrees with the log line the script greps.
Source: `F:\Godot\docs\class-ref-xml\doc\classes\SceneTree.xml` (`quit`). Seen: 0 here (seeded from delve-the-dungeon).

## Housekeeping

### A doc comment describes the code as it is now
<!-- rule: doc-comment-matches-code -->

A summary, a `<returns>` or a rationale is checked against the code in the same diff: "presses the very button" above an `EmitSignal`, "the engine keeps no reference" above a `RefCounted`, and "true when the command went" above a method that returns true regardless were each a finding. When the fix round rewrites a rationale, the new one is verified too; the wrong one is not replaced with another wrong one.
Source: global `CLAUDE.md` (class summaries describe the current feature). Seen: 0 here (seeded from delve-the-dungeon).

### Delete the dead code a change leaves behind
<!-- rule: delete-dead-code -->

A replacement removes its predecessor: the branch a shortcut now consumes, the filter clause a caller's guard makes redundant, the fallback no call site can reach, the reset after a `Quit()`. A finding says which; the fix round deletes it rather than commenting it.
Source: global `CLAUDE.md` (replace, don't deprecate; flag dead code); `F:\Godot\docs\manual\tutorials\inputs\inputevent.rst` for input-order dead branches. Seen: 0 here (seeded from delve-the-dungeon).

### No bridge override left after a drive
<!-- rule: no-mcp-bridge-at-commit -->

The godot MCP server injects its bridge through an `override.cfg` beside `project.godot` whose first line is `; godot-mcp: bridge injection, removed when the run stops`. `.git/info/exclude` hides it, so `git status` never shows it, and `stop_project` or `detach_project` removes it; a crashed or killed run can leave it behind, and a leftover one loads the bridge autoload into every later run of the project, headless test runs included. After every drive, `Test-Path <client project>/override.cfg` is false; a leftover whose first line is that marker is deleted, and one without it is the project's own and is left alone.
Source: the project's `CLAUDE.md` (its note on the MCP bridge). Seen: 0 here (seeded from delve-the-dungeon).

### Hoist a side effect out of a condition
<!-- rule: hoist-side-effect-from-condition -->

A call that opens a tab or a panel, placed in a `foreach` condition or as the last term of an `||` chain, moves what is visible for items the loop then skips. A call that changes what the player sees is its own statement after the guards.
Source: none in the cache. Seen: 0 here (seeded from delve-the-dungeon).

### Name a method by what it does
<!-- rule: name-method-by-what-it-does -->

A method is an imperative verb and its object: `SetSpeed`, `RebuildRows`, `OpenFirstTurn`, `GrabFocus`. A bare verb takes its class as the subject, so it is right only when the class is what the verb acts on (a map's `Build` that builds only its tower is `BuildTower`; an effects player's `Stop` that stops one effect is `StopEffect`). An accessor that does work is `Get`/`Set` + noun (`GetSpeed`); a trivial one is a property. A handler is `On` + source + signal (`OnLockPressed`, `OnGameChanged`). A predicate is `Is`/`Has`/`Can`, and it is pure: a log line, a close or a save on either path is a statement of its own at the call site, hoisted out of the condition. A handler keeps the whole signal name (`OnNameFieldTextChanged`, `OnPresetPickerItemSelected`, not `OnNameFieldChanged`), and it is reached only from its signal: code that wants the same work calls a verb method the handler forwards to, and a delegate a constructor takes is named for the work, not as a handler (`SettleAfterPlayback`, when the object that calls it raises no signal). A script method never takes the name of an engine property's accessor on its base (`SetName`, `SetVisible`, `GetName` on a `Node`): the source generator registers the script method under that name and hides the engine's `MethodName` constant, so a label's writer is `WriteNameLabel` and a child's visibility is `SetControlVisible`. A verb taking a `bool` that means the opposite half the time is a `Set<Noun>` (`SetLifted(held)`, `SetButtonEnabled(button, enabled)`, never `Lift(false)`). Async ends in `Async`. A past participle names a signal or an event and nothing else (`Pressed`, `Settled`, `RoundStarted`), because it says something happened; on a method it makes a setter read as a handler and hides the verb (`Speeded`, `Rose`, `Told`, `Regrabbed` are findings). Pure static computations keep the .NET helper shape (`TierOf`, `Wording.Of`): a noun phrase, `<Noun>Of`, or an imperative verb (`Harden`, `Draw`), and never a past participle even when pure (`Hardened`, `Withheld`, `Drawn` are findings). A test's scenario builders and helpers follow the rule (`BuildGuardedScenario`, never `Guarded`); only test methods themselves follow the project's test-naming rule instead. A brief for a file still carrying old names renames what it touches rather than adding to them.
Source: `F:\Godot\docs\manual\tutorials\scripting\c_sharp\c_sharp_style_guide.rst` (PascalCase methods; signals named in the past tense as `EventHandler` delegates) and Godot's own API (`AddChild`, `SetProcess`, `Pressed`). Seen: 0 here (seeded from delve-the-dungeon).

### Private fields carry no underscore prefix
<!-- rule: no-underscore-private-fields -->

The Godot C# guide prefixes private fields with `_`; the owner's projects do not. The project says what holds the convention (`.editorconfig`, or the reviewer alone); either way the reviewer records the deviation from the guide rather than raising it, and raises a prefix that appears. Do not adopt the prefix in one file. Held by `.editorconfig`: a naming rule makes a private field camelCase with no prefix, with `private const` and `private static readonly` fields carved out as PascalCase, and `dotnet_diagnostic.IDE1006.severity = warning` is what makes the build enforce it, since a naming rule's own `severity` is read by the IDE alone (measured: an underscore field built green without the key). Where a renamed field meets a parameter or local of the same name, qualify the field with `this.`. MSBuild's up-to-date check does not read `.editorconfig`, so a changed naming rule is proven with a rebuild (`-t:Rebuild`).
Source: `F:\Godot\docs\manual\tutorials\scripting\c_sharp\c_sharp_style_guide.rst`. Seen: 0 here (seeded from delve-the-dungeon).
