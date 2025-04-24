# MathVR

MathVR is a VR productivity app designed to create a convenient and immersive study space where students and teachers can come together to learn and collaborate on math problems. Built with Unity and primarily using C#, MathVR transforms traditional math learning into an engaging, interactive, and accessible experience. 

## Features

- **Immersive VR Classroom**  
  Enter a virtual classroom featuring a shared whiteboard at the front. Write out equations and click 'Submit' to have them solved instantly, making complex math concepts more approachable.

- **Multiplayer Collaboration**  
  Join classmates and instructors in real time within the same virtual space. Collaborate, interact, and solve problems together, fostering teamwork and group learning.

![Alt text](https://github.com/TylerP2405/MathVR/blob/main/math-vr-home-screenshot.png?raw=true)

## Equation Processing Workflow

MathVR uses an automated pipeline to process and display math solutions:

- **Whiteboard to LaTeX:**  
  The whiteboard image is converted to LaTeX using the Mathpix API.

- **Solving with Gemini API:**  
  The LaTeX equation is sent to the Gemini API, which returns the solved result in LaTeX format.

- **PDF Generation:**  
  The solution LaTeX is sent to LaTeXOnHTTP to generate a PDF.

- **Image Display:**  
  The PDF is locally converted to an image using [PDFtoImage](https://github.com/sungaila/PDFtoImage/tree/master), allowing the solution to be displayed directly in VR.

## Networked Drawing Workflow

A multiplayer whiteboard enables multiple users to draw, write, and interact on a shared virtual canvas in real time:

- **Synced Drawing:**
When a user draws, their device detects the brush position and sends the drawing data (as a “DrawPoint”) to other users using networked Remote Procedure Calls (RPCs). This data includes coordinates, color, size, and rotation.

- **Buffering for Performance:**
Incoming drawing data from other users is stored in a local buffer. To prevent overloading the system, only a set number of drawing points are processed per frame (e.g., 1000), ensuring smooth performance even during rapid collaboration.

- **Full History Replay:**
When a new participant joins an ongoing session, they request the complete drawing history from the host or authoritative client. The host sends all previous drawing points, typically in batches to avoid network congestion.

- **State Catch-Up:**
The late joiner’s whiteboard processes this history buffer, quickly redrawing all prior content so their view matches the current state seen by other users.

## Getting Started

### Prerequisites

- Unity (recommended version: ver. 6000.0.36f1)
- VR headset compatible with Unity XR (Oculus Quest)
- .NET environment for C# development

### Installation

1. Clone the repository:
`git clone https://github.com/TylerP2405/MathVR.git`
2. Open the project in Unity.
3. Connect your VR headset and ensure the necessary drivers and SDKs are installed.
4. Build and run the project from Unity.

### Usage

- Launch MathVR and enter the virtual classroom.
- Use the VR controllers to write on the whiteboard, submit equations, and interact with 3D objects.
- Invite classmates or teachers to join your session for collaborative learning.

## Contributing

We welcome contributions! Please fork the repository and submit a pull request for review.

## Contributors

- TylerP2405
- Gordon Nguyen
- Jake Schwartz
- Mysha-rah
- Erin Bartels
- Sean Pletan
- Sally Alakeel
- Andrea Poklar

## License

This project is licensed under the MIT License.

---

*Repository languages: C#, ShaderLab, C, C++, CMake, JavaScript, Other*  
*Repository: [MathVR](https://github.com/TylerP2405/MathVR)*
