===========================================================
    CyberWenwan Official Mod SDK Guide
===========================================================

Thank you for contributing to the CyberWenwan Workshop! This tool is designed to help artists quickly import 3D models into the game and upload them to Steam with zero coding required.

-----------------------------------------------------------
1. Environment Setup
-----------------------------------------------------------
- Unity Version: Must use 2022.3.62f3c1 LTS.
- Steam Client: Steam must be logged in and running during the creation and upload process.
- Asset Preparation: 
    * Model: FBX format recommended. The center point (root) must be exactly at the world origin (0,0,0) with a scale of 1.
    * Texture Naming Convention (Crucial):
        _TexA: Base map (Before patina/evolution)
        _TexB: Base map (After patina/evolution)
        _Normal: Normal map (Tool will auto-convert the format)
        _Mask: Mask map (Determines which areas change color first)
        _Metallic: Metallic map (Leave empty if not metal)
        _Icon: Preview Icon (Square PNG)

-----------------------------------------------------------
2. Quick Start
-----------------------------------------------------------
Step 1: Import Assets
- Click the top Unity menu: Tools -> CyberWenwan -> 一键究极资产导入与上传 (Ultimate Importer & Uploader).
- Click "从外部文件夹导入素材" (Import from External Folder) and select the folder containing your FBX and textures.
- Set a "Unique Species ID" (English letters and numbers only, e.g., walnut_my_01).

Step 2: Generate & Preview
- Adjust "Default Material Parameters" and "Play Difficulty Settings".
- Click "第一步：生成游戏资产并打包" (Step 1: Generate & Build Bundle).
- Once the progress bar finishes, you can click the Play button at the top of Unity to preview the patina effect in the scene in real-time.

Step 3: Upload to Workshop
- In the Steam module at the bottom of the tool panel, enter your Mod Title and Description.
- Select an image as the preview cover.
- Click "确认上传到 Steam" (Upload to Steam Workshop).

-----------------------------------------------------------
3. Modding Standards
-----------------------------------------------------------
- Single: The FBX contains one independent model.
- Dual: The FBX must contain independent 'Left' and 'Right' child objects. Do not merge them.
- String: All beads should be arranged around the origin as child objects. The system will automatically calculate collisions.
- Material: You must use the included "WenwanEvolution" shader to ensure the patina system works correctly.

-----------------------------------------------------------
4. FAQ
-----------------------------------------------------------
Q: The upload button is grayed out or says Steam initialization failed?
A: Ensure your Steam client is open and running, and that the `steam_appid.txt` file exists in the SDK root directory.

Q: The material on my imported model is completely black?
A: Check if "Auto Add Reflection Probe" is enabled in the tool, or try assigning an HDR image to the "Custom Reflection Texture" slot.

Q: I subscribed to my Mod, but it doesn't show up in the game?
A: After a successful upload, please go to your Steam Workshop page and change the Mod's visibility to "Public".

===========================================================
Happy Modding!