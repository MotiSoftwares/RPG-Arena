# Unity Course – Starter Repository
Blank Unity starter repository, defaulted to Unity version 2022.3.62f3
This repository is the **starting point** for your Unity course project.  
Students create their Unity project inside this repo.

> [!warning]
> Complete project instructions & guidelines are found at **instructions.md**, please follow them carefully!

## Requirements
- Unity version: **2022.3.62f3 (recommended)**. Other versions are allowed only with permission.
- Git ≥ 2.4
- [Git LFS](https://git-lfs.com/) installed (`git lfs install`)

## How to use
1. Install Git LFS (once per computer):  
   `git lfs install`
2. Clone this repository.
3. Create your Unity project inside this repo.
4. Commit your changes regularly.
5. Before submitting:
   - Ensure your final commit is on **main** (or **master**).

## Submission (Partial)
- Formality: Submit the **GitHub repository URL** in Moodle.
- Only the **main/master branch** will be evaluated.
- **Continue by following the complete submission guide found in the project instructions!**

### Pre-Submission Project Testing Checklist
1. **Clone on a fresh machine**
	Clone (pull) your team repository to a different computer (or delete your local copy and re-clone). Open the project to confirm there are no missing scripts or import errors.

2. **Build test**
	Add all necessary scenes to Build Settings and create Windows/WebGL builds. Launch them to ensure the game runs correctly and the UI scales properly.

3. **Save/load verification**
	If the project uses save files, create a save, then clone the repo on another device and verify the save loads without errors.

4. **Repository size check**
	Run `git lfs status` to ensure all large files are tracked by LFS. Confirm that build folders have not been committed accidentally.

5. **Review README and instructions**
	Ensure the project includes clear instructions: how to run the game, controls, how to access any debug features, and any other testing guidelines.

6. **Moodle group verification**
	Make sure your group is correctly set up in Moodle. Only one member needs to submit the Classroom link on Moodle; double-check you’re not overwriting another group’s submission.

7. **Review the full project instructions**
	- Before submitting, carefully review the full project instructions and confirm that your project addresses the required criteria as completely as possible, including all relevant items in the **Submission Sanity Check** section.

## Notes
- Create your Unity project inside this repo (this folder becomes the Unity project root).
- Large assets must be committed via **Git LFS** (this repo is pre-configured).
   - Two LFS placeholder files have been placed into the repo to encourage a **system LFS initialization prompt**.
     - **You may delete** or make use of them: **transparentPixel.png** **whitePixel.png**
- Do not commit build artifacts or Library/Temp folders.
- Later replace this README.md file with your own version as per the project instructions.
   - Use the provided README template
- **Important:** since you'll be granted **admin** access to your own Classroom Repositories, **DO NOT**:
   - delete the repository
   - rename the repository
   - delete the `main` / `master` branch
   - modify tags after submission
   - change repository visibility
   - detach the repository from GitHub Classroom

## FAQ
**1. Is it okay to fork or import the Classroom repository?**
	**No.** Forking or importing creates a separate repository disconnected from your team assignment. Always use the repository automatically created by GitHub Classroom.

**2. What files should never be committed?**
	Avoid committing build binaries (e.g., compiled `.exe` or WebGL build folders), `Library/`, and `Temp/` folders. Use Unity’s recommended `.gitignore` and configure `.gitattributes` with Git LFS for large binary files (e.g., `.png`, `.jpg`, `.psd`, `.fbx`, `.wav`).

**3. When should we use Git LFS?**
	Large binary assets such as textures, models, and audio files should be tracked using Git LFS. This keeps your git history lightweight and avoids repository bloat.

**4. How can we avoid merge conflicts?**
	Use branches for separate features. Always `git pull` before starting work, and open a pull request when merging. Resolve conflicts before merging.

**5. What happens if we accidentally push our Unity project into another Unity project?**
	Never nest Unity projects. Create a clean new Unity project and then copy your `Assets` and `ProjectSettings` folders into it.

**6. How do we manage Unity assets responsibly?**
	Do not store third-party packages or downloaded assets inside another project’s directories. Keep each project self-contained.
