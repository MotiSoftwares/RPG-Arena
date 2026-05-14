# Student Choice Course Project: Instructions

---

**Homework Assignment**: *Unity Course Project – Student Choice* (covers **Unity Development Topics** from Lectures)  
**Due Date**: *Check GitHub Classroom repository's deadline*

**Objective**:  
To practically apply the Unity development skills learned throughout the semester by developing a small, fully-functional game of your choosing, demonstrating proficiency in Unity basics, gameplay mechanics, UI design, and overall project management.

---

**Task Description**:  
- Form teams of **3-5 students**. Exceptions are allowed only with permission.

- Develop a game of your choice, subject to instructor approval, meeting the general criteria:
  - **Scenes/Levels**: 2-3 unique scenes or levels.
  - **Gameplay Duration**: Approximately 5-15 minutes for a complete playthrough.
  - **Clear Start and Finish Conditions**: Defined objectives or win/lose conditions.
  - **Intuitive User Interface (UI)** and Heads-Up Display (HUD).
  - **Asset Handling**: At least 10 distinct assets (sprites, models, audio).
  - **Basic Interaction Mechanics**: At least 3 different types (player input, collision detection, event triggers).
  - **Gameplay mechanics/features**: A minimum of 5 distinct and moderately complex gameplay mechanics/features.
  - **Scene Management and Transitions**: Smooth and logical scene transitions.
  - **Unity Animation System**: Minimum 2 animated objects or characters.
  - **Game Progression**: Basic scoring or progression system (levels, points, health, etc.).
  - **Visual/Gameplay Polish**: Minimum 1 polished element (particle effects, camera movements, transitions).

- The scope should remain manageable, considering other academic responsibilities.

- Review grading rubric below!

---

## **Baseline MVP Expectations**

In addition to the formally graded gameplay mechanics/features, each project is expected to demonstrate a complete and usable minimum game structure.

These expectations represent the basic level of project completeness expected from a Unity course project. They do not automatically count as gameplay mechanics/features, but they may affect the final grade through functionality, polish, code quality, UX, conceptual understanding, and overall project completeness.

Poor or missing implementation of these baseline systems may negatively affect the final grade, especially if it harms the usability, stability, or professionalism of the game.

### **Required Baseline Game Flow**

Each project should include a clear and functional game flow:

- **Main Menu Scene**
  - Play / Start Game
  - How to Play / Instructions
  - Settings, where appropriate to the game
  - Credits
  - Exit / Quit Game

- **Gameplay Scene(s)**
  - The actual playable game experience
  - Complete working controls appropriate to the game
  - Clear objective, progression, win condition, lose condition, or end state

- **Pause Menu**
  - Proper pause logic
  - Resume
  - Restart
  - Settings, where appropriate
  - Exit to Main Menu

- **Game Over / End State**
  - Clear indication that the game has ended, whether by winning, losing, or completing the objective
  - Option to restart, return to the main menu, or exit
  - This may be implemented as a separate screen or integrated into the pause/menu system, as long as the logic and UX are clear

At minimum, the project should not place the entire game into a single scene. The main menu and gameplay should be separated into at least two different scenes, with proper scene transitions.

### **Controls and Player Usability**

The game should include a complete and working control scheme appropriate to the chosen game type.

Controls may include keyboard, mouse, controller, or any combination relevant to the game. The controls should be responsive, understandable, and documented clearly in the README using the provided README template and/or in the game's How to Play screen.

The player should not need to guess how to start, play, pause, restart, or exit the game.

### **Settings, Audio, and Feedback**

If the game includes sound, it should use a reasonable audio structure, such as Audio Mixers, instead of disconnected or hardcoded audio behavior.

If the game includes animation, animated objects or characters should use a proper Animator Controller and state machine where appropriate, rather than relying only on disconnected animation clips or manual animation triggers.

The game should provide appropriate feedback to the player, where relevant. This may include button hover/click feedback, sound effects, UI prompts, hit feedback, pickup feedback, damage feedback, success/failure messages, or other forms of clear player communication.

### **UI and HUD Expectations**

The game's UI and HUD should be readable, functional, and appropriate to the game.

UI should preferably be resolution-responsive and should not break, overlap, or become unreadable under common screen resolutions.

HUD elements should support gameplay clarity, not merely exist as visual decoration.

### **Technical Stability and Runtime Quality**

The project should run cleanly and reliably.

Students should aim to submit a project with:

- no critical errors
- no repeated console errors
- no excessive warning spam
- no broken references
- no missing scenes
- no missing assets
- no major performance issues
- no uncontrolled infinite spawning or memory leaks
- no unnecessary accumulation of unused GameObjects during gameplay

Use of `DontDestroyOnLoad`, singleton managers, static data, or persistent systems should be handled carefully to avoid duplicated managers, broken scene transitions, or inconsistent game state after restarting.

Clean and responsive gameplay is expected to an acceptable degree. A project does not need to be commercially polished, but it should feel stable, playable, and complete as a student course project.

### **Build and Editor Behavior**

The project should work both in the Unity Editor and in a built version, unless a specific limitation is clearly documented and approved.

Exit / Quit behavior should be handled correctly:

- In a built version, the Exit button should quit the application.
- In the Unity Editor, the Exit button should stop play mode or otherwise be safely handled for testing.

All required scenes should be properly added to the Build Settings.

### **Submission Sanity Check**

Before submitting, students should verify that:

- The project opens correctly from a clean clone of the repository.
- The correct starting scene is included and works.
- All required scenes are included in Build Settings.
- The game can be played from start to finish.
- The provided README template has been revisited and completed according to the submission instructions.
- There are no missing scripts, missing prefabs, missing materials, missing references, or broken serialized fields.
- The project does not require the instructor to install optional, unused, or unnecessary packages, Asset Store tools, or editor extensions in order to open, run, build, or evaluate the game. Any required dependency must be essential to the submitted project, clearly included/configured where possible, and documented if it is part of an approved functional integration, such as the External Framework / API Integration bonus.

### **Project Completeness Reminder**

Baseline systems such as menus, pause logic, settings, audio control, UI responsiveness, and scene flow are part of making the project feel complete and usable.

However, these systems do not replace the requirement for distinct, moderately complex gameplay mechanics/features. A complete menu system may improve the overall project quality, but it does not count as one of the required gameplay mechanics unless it directly affects gameplay state, player decisions, progression, challenge, or interaction in a meaningful way.

---

* **Cheat Manager (Highly Recommended Bonus +5 points)**:
  * Implement a cheat manager for instructor and student use during testing and debugging.
  * A cheat manager is a system that allows quick access to debugging/testing functions (e.g., teleporting, invincibility, level skipping).
  * The logic behind having a cheat manager is to streamline testing, facilitate debugging, and efficiently demonstrate project functionality.
  * Provide a streamlined & easy-to-use interface within the running game for dynamically accessing cheat functionalities.
  * Must only be compiled within the development build context and the Unity Editor. Use conditional compilation directives such as `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.

---

* **External Framework / API Integration (Bonus up to +10 points)**:
  * To encourage deeper technical skill and tool adoption, teams may implement **one external non-Unity gameplay framework, logic system, narrative tool, or API** in their game project.
  * **Requirements, the integration must:**
    * Be cleanly and correctly implemented.
	* Solve a real functional/gameplay requirement.
	* Interact meaningfully with your game logic.
	* Not be purely cosmetic or unused boilerplate.
	* Be documented in the README, Documentation must include:
	  * Why this framework was chosen.
	  * How it was integrated.
	  * How it interacts with your game systems.
	  * Demonstration of meaningful impact on gameplay, UI, narrative, logic, or systems.
  * Frameworks used only as placeholder decorations, unused stubs, or surface-level “plug-in” features will **not qualify**.

> [!note]
> ### Framework / API Example:
> 
> **Ink by Inkle Studios** (Narrative scripting system for Unity)
> 
> A valid 5-point Ink implementation could include:
> 
> * branching narrative choices
> * conditional dialogue paths
> * persistent variables affecting logic
> * dialogue influencing gameplay state
> * node-based sequence progression
> * interactions triggering story flags
> 
> > Exceptional frameworks (deep gameplay connection, multi-system integration, behavioral logic, data-driven systems, etc.) may receive **up to 10 points**.
> 
> > Qualifying examples (not limited to):
> 
> * Narrative systems (Ink, Yarn Spinner, Naninovel)
> * Behavior logic frameworks
> * AI decision frameworks
> * Procedural map/level systems
> * Graph-based logic frameworks
> * Data-driven toolkits

---

### Gameplay Mechanics Definition:

  * Gameplay mechanics/features refer specifically to interactive elements or systems that directly impact player actions, decisions, or game state progression (e.g., character movement, puzzle-solving, combat, resource management). UI elements, menus, or visual-only elements (like HUDs) do not count toward this requirement.
  * Each gameplay feature should demonstrate a moderate complexity level suitable for an intermediate Unity course (e.g., implementing character controls, simple enemy AI, puzzle mechanisms).
  * Required: Minimum of **5 distinct and moderately complex gameplay mechanics/features**.

  * ## **Complexity Examples**:

    * ### **Moderate Complexity**:

      * **Dialogue Trigger System**: NPC interactions with branching dialogue choices, influencing small state variables or unlocking new areas/events.
      * **Staged Puzzle Mechanic**: Multi-phase puzzles requiring sequential actions or problem-solving across different areas, including clear feedback for player progress.
      * **Ability Charging or Cooldown System**: Player ability with hold-to-charge mechanics or cooldown timers, accompanied by clear visual indicators or animations.
      * **Object Pooling System**: Fully functional object pooling system logically integrated into the game, optimizing performance for frequently instantiated and destroyed objects (e.g., projectiles, enemies, particles).

    * ### **Basic Complexity**:

      * A coin or pickup system that increases score on collision.
      * A basic jump mechanic using Rigidbody.
      * A physical in-world button that triggers a platform to rise.

    * ### **Minimal Complexity**:

      * Static background props or decorations.
      * Objects that play an animation without any condition.
      * Simple movement using built-in components with no added logic.

### Gameplay Feature Grading Clarification:

* Minimal implementation elements are **not considered gradable gameplay mechanics/features** on their own.
* This includes, but is not limited to: looping background music, simple trigger-based sound effects, HUD numbers that update without deeper gameplay logic, simple timers, primitive ScriptableObject data containers, simple rotating objects, or any similarly low-complexity implementation.
* These elements may still be useful and appropriate as part of a complete game. However, they are considered supporting implementation details, polish, feedback, or presentation elements — not primary gameplay mechanics/features.
* Students should not count such elements toward the required gameplay mechanics/features. A gradable gameplay mechanic/feature must meaningfully affect player interaction, decision-making, game-state progression, challenge, or gameplay feedback.

---

**How to Submit**:
> [!warning]
> ## Submission Instructions for Unity Course Project
> 
> ### 📌 Submission via GitHub Classroom
> 
> > **All projects must be submitted through your assigned GitHub Classroom repository.**
> > This is the official submission location and determines both:
> > - the graded commit
> > - the deadline timestamp
> 
> ### Step 1: GitHub Classroom Repository Submission
> 
> - Your project must exist in the **team repository** automatically created by GitHub Classroom.
>     
> - Repositories not tied to the official Classroom assignment will **not** be accepted.
> 
> - The Classroom submission timestamp is the official deadline marker.
> 
> - The evaluation will **only consider the `main` or `master` branch** of your repository.
> 
>     - Make sure your project is finalized on this branch.
> 
> ### Step 2: README.md
> 
> - Prior to final submission, overwrite the repository's README.md file using your provided template.
> 
> - Please remember to include:
> 
>     - A YouTube link to a short recorded video (5-10 minutes maximum), showcasing gameplay & project.
> 
>         - Narrate the video, explaining about what is happening and about the project's inner workings.
> 
>     - A few screenshots highlighting key elements of the game.
> 
> ### Step 3: Moodle Link (Formality Only)
> 
> - Submit your repository link on Moodle as requested.
>     
> - This exists purely for **administrative tracking**, and not as the actual submission.
> 
> ### Step 4: Group Submission
> 
> - This assignment is a **group activity**:
>     
>     - Confirm you're properly assigned to your **correct group**.
>         
>     - Students **without a group** will **not be able to submit**.
>         
> - **Submission made by one group member applies to the entire group**:
>     
>     - Only **one submission per group** is required.
>         
>     - Be cautious: making multiple submissions or incorrect group assignments can overwrite previous submissions. Ensure clear communication within your group before submitting.
>         
> - **Warning**:
>     
>     - Incorrect or missing group assignments may lead to **no grade** or an **incorrect grade** being awarded.
> 
> ---
> 
> Please carefully follow these instructions to avoid any submission errors or penalties. If there are any uncertainties or issues, please reach out immediately before the submission deadline.

---

**Rubric**: (Total **100** points + Bonus) – *The grading criteria are outlined below:*

| Criteria                                                       | Excellent (100%)                                                                                                                                                                      | Good (80%)                                                                                                             | Satisfactory (60%)                                                                 | Needs Improvement (0-40%)                                          | Points |
| -------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- | ------------------------------------------------------------------ | ------ |
| **Functionality (30 pts)**                                     | Fully functional, meets all specified criteria, no bugs/issues.                                                                                                                       | Mostly functional, meets most criteria, minor bugs/issues.                                                             | Basic functionality, meets core criteria, some noticeable issues.                  | Significant issues or missing key features.                        | 30     |
| **Gameplay Mechanics (20 pts)**                                | Implements 5 distinct and moderately complex gameplay mechanics clearly impacting game interactions.                                                                                  | Implements 5 distinct mechanics with minor issues or simplicity.                                                       | Implements at least 5 mechanics of basic complexity.                      | Lacks distinct or functional gameplay mechanics.                   | 20     |
| **Code Quality & Readability (15 pts)**                        | Organized, readable code consistently following style guidelines.                                                                                                                     | Mostly organized, minor readability or style issues.                                                                   | Readable but somewhat inconsistent or unclear.                                     | Poorly organized, unclear, difficult to follow.                    | 15     |
| **Documentation (15 pts)**                                     | Thorough README/documentation covering setup, gameplay, controls, assets, and management.                                                                                             | Clear documentation with minor gaps or omissions.                                                                      | Basic documentation, limited detail or clarity.                                    | Missing or severely lacking.                                       | 15     |
| **Conceptual Understanding (10 pts)**                          | Clear demonstration/explanation of Unity concepts (assets, interactions, animations, transitions, etc.).                                                                              | Good understanding with minor gaps or limited explanations.                                                            | Basic understanding, occasionally unclear or incomplete explanations.              | Minimal or incorrect understanding shown.                          | 10     |
| **Project Management (10 pts)**                                | Effective use of project management tools, clear role definitions, consistent task tracking.                                                                                          | Good management, minor tracking or clarity improvements possible.                                                      | Basic management, evident issues in tracking or roles.                             | Poor management, unclear roles, minimal or no tracking.            | 10     |
| **Creativity & Extra Effort (Bonus up to +10 pts)**            | Significant creative enhancements or additional engaging gameplay features.                                                                                                           | Moderate creative enhancements beyond basic criteria.                                                                  | Basic extra feature with limited creativity.                                       | Minimal or no extra effort.                                        | +5-10  |
| **AI Implementation (Bonus up to +10 pts)**                    | Effective use of Unity AI systems (e.g., NavMesh, simple AI behavior).                                                                                                                | Basic AI implementation that enhances gameplay but with minor issues.                                                  | Simple AI included with minimal gameplay impact.                                   | No AI features implemented.                                        | +5-10  |
| **Cheat Manager (Bonus +5 pts)**                               | Fully functional cheat manager, clearly restricted to Editor/Dev builds, robust set of debugging options.                                                                             | Basic functional cheat manager, minor limitations.                                                                     | Minimal cheat manager implementation with limited options.                         | No cheat manager implemented.                                      | +5     |
| **External Framework / API Integration (Bonus up to +10 pts)** | Clean, technically-sound integration deeply connected to gameplay, narrative, logic, progression, or systems; strong documentation and justification; demonstrably elevates the game. | Complete functional integration with meaningful but limited scope, minor logical shortcomings, adequate documentation. | Partial integration, unclear implementation value, minimal gameplay/system impact. | No framework/API used, or implementation is hollow/non-functional. | +5-10  |

**Total Points:** 100 (+35 bonus available)  

---

> **External Framework / API Integration Note:**
> The bonus does not stack across multiple frameworks — only the most significant, well-implemented system will be evaluated

**Feedback Plan**:  
Detailed written feedback provided along with rubric scoring. Individual or group feedback sessions available upon request for further guidance.

**Academic Integrity**:  
All submissions must be original group work. Cite external resources clearly. Collaborative work should reflect fair team contributions.

> **Tip for Students:**  
> Break down the project into manageable tasks using tools like Trello, Jira, or Obsidian notes. Regularly schedule team meetings to track progress, discuss tasks clearly, and test features incrementally before integration.