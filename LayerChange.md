**You might need to change the layer number to not conflict with other system already in your world**

## **To change the layer number of liquidSystem, you'll need to:**
0. Make sure you do this first before putting any prefabs into the world, or reimport them afterwards
1. Create a new layer as described previously or choose a non-conflicting layer
2. Open the Glass-REV2_VER2 prefab and edit these 4 GameObjects *(GlassParticleCollider, ParticleSystem_SizeVariable, ContainerCollider and ContainerColliderOnlyParticles_Glass)*

![collisionLayerChange1](https://user-images.githubusercontent.com/81592952/118156044-2dc50200-b419-11eb-871d-5a2bf0ca8738.png)

3. For the particle collider just change the GameObject's layer to the new desired layer
4. For the Particle Systems, go to the each of the Particle Systems' Collision module, deselect the old layer (in the example called *"somethingElseOnLayer23"*) and select your new desired layer. Make sure to not change any other layers that might be present

*Example for ParticleSystem_SizeVariable:*

![collisionLayerChange2](https://user-images.githubusercontent.com/81592952/118157267-9fea1680-b41a-11eb-852c-1f99d1c67eae.png)

*(On the other Particle Systems only one layer should be selected)*

5. Open the Liquid_Spout prefab and edit these 3 GameObjects *(Liquid_Spout, ContainerCollider and ContainerColliderOnlyParticles_Spout)* as described in **Step 4**

![collisionLayerChange3](https://user-images.githubusercontent.com/81592952/118157553-fa837280-b41a-11eb-8497-4a17c60892ea.png)

**Now you can proceed to the rest of the guide**
