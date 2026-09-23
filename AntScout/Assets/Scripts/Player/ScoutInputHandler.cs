using System;
using AntScout.Core.Enums;
using AntScout.Core.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AntScout.Player
{
    /// <summary>
    /// Translates player input from Unity's New Input System into 3D locomotion, dash evasion, and pheromone emission.
    /// Strictly adheres to Single Responsibility Principle (SRP) by decoupling input gathering from gameplay execution.
    /// </summary>
    public class ScoutInputHandler : MonoBehaviour
    {
        private IMotor _motor;
        private IDashable _dash;
        private ScoutGland _gland;
        private Vector2 _currentMoveInput;

        private void Awake()
        {
            _motor = GetComponent<IMotor>();
            _dash = GetComponent<IDashable>();
            _gland = GetComponent<ScoutGland>();

            if (_motor == null)
            {
                throw new InvalidOperationException(
                    $"[ScoutInputHandler] Missing required IMotor component on GameObject '{gameObject.name}'.");
            }

            if (_dash == null)
            {
                throw new InvalidOperationException(
                    $"[ScoutInputHandler] Missing required IDashable component on GameObject '{gameObject.name}'.");
            }

            if (_gland == null)
            {
                throw new InvalidOperationException(
                    $"[ScoutInputHandler] Missing required ScoutGland component on GameObject '{gameObject.name}'.");
            }
        }

        private void Update()
        {
            ProcessLocomotionInput();
            ProcessDashInput();
            ProcessGlandInput();
        }

        private void ProcessLocomotionInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            Vector2 moveDir = Vector2.zero;

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveDir.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveDir.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveDir.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveDir.x -= 1f;

            if (moveDir.sqrMagnitude > 1.0f)
            {
                moveDir.Normalize();
            }

            _currentMoveInput = moveDir;
            _motor.SetMoveInput(moveDir);
        }

        private void ProcessDashInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                Vector3 dashTargetDir = _currentMoveInput.sqrMagnitude > 0.01f
                    ? new Vector3(_currentMoveInput.x, 0f, _currentMoveInput.y)
                    : _motor.CurrentVelocity;

                _dash.TryDash(dashTargetDir);
            }
        }

        private void ProcessGlandInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            bool isRightClick = mouse != null && mouse.rightButton.isPressed;
            bool isShiftHeld = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            bool isAlarmHeld = keyboard != null && keyboard.eKey.isPressed;

            if (isAlarmHeld)
            {
                _gland.SetLayingTrail(true, PheromoneType.Alarm);
            }
            else if (isRightClick || isShiftHeld)
            {
                _gland.SetLayingTrail(true, PheromoneType.Recruitment);
            }
            else
            {
                _gland.SetLayingTrail(false);
            }
        }
    }
}
