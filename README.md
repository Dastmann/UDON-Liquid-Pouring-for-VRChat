# UDON-Liquid-Pouring-for-VRChat
UDON Liquid Pouring system for VRChat by Dastmann

Liquid Pouring adds a decently realistic liquid *simulation* for pouring.
It allows you to pour liquid into a glass, pour it out, pour it between glasses amd mix different colors together!

[Showcase world](https://vrchat.com/home/launch?worldId=wrld_c578d49b-c7c1-4424-8e77-fff182f13c6e)


# How to set up:

0. Have your VRCSDK3-WORLD and [UdonSharp](https://github.com/MerlinVR/UdonSharp) already imported in your project. 

1. Go to any GameObject and click the Layer drop-down in the top right of the Inspector. Then click on "Add Layer...".

![layerSetup1](https://user-images.githubusercontent.com/81592952/118041402-09aee580-b373-11eb-886b-40dcb1cfc6a0.png)

2. Create a new layer "liquidSystem" on the layer 23. [How to change layer](LayerChange.md)

![layerSetup2](https://user-images.githubusercontent.com/81592952/118041703-66aa9b80-b373-11eb-90b5-39623f7ab5ec.png)

3. Go to "Edit -> Project Settings... -> Physics" and scroll down to the Layer Collision Matrix.

![collisionSetup1](https://user-images.githubusercontent.com/81592952/118041951-be490700-b373-11eb-996e-283555eb41f3.png)

4. Deselect these layers under the liquidSystem Layer: Default, Player, PlayerLocal and Pickup.

![collisionSetup2](https://user-images.githubusercontent.com/81592952/118042124-f8b2a400-b373-11eb-86eb-13188a432461.png)


To use the provided script for toggling the Liquid Spout on or off, you have just to:
1. Put the "spoutSync.cs" script on the GameObject you want to be the trigger (It behaves like a normal Interact)
2. In the inspector click "Convert to UdonBehaviour"
3. And provide a reference to the Liquid_Spout GameObject in you scene

Otherwise you can use any method of turning the Liquid_Spout GameObject on or off.
Currently the toggle has to be global, othewise other players won't see the liquid level changing.


## **Have Fun!**
