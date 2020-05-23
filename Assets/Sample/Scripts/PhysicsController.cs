﻿using UnityEngine;

public class PhysicsController : MonoBehaviour
{
    [SerializeField]
    float timestep = 0.01f;

    private void Awake()
    {
        Time.fixedDeltaTime = timestep;
    }
}
