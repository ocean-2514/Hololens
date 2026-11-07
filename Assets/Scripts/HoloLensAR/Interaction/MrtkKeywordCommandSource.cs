using System;
using System.Collections.Generic;
using HoloLensAR;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using MixedReality.Toolkit;
using MixedReality.Toolkit.Subsystems;

namespace HoloLensAR.Interaction
{
    /// <summary>
    /// Adapts MRTK's running keyword-recognition subsystem to application commands.
    /// It never edits a model Transform or stores recognized speech text.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MrtkKeywordCommandSource : MonoBehaviour, IKeywordCommandSource
    {
        [Serializable]
        private sealed class KeywordMapping
        {
            public string keyword;
            public SimulationCommand command;

            public string Keyword { get { return keyword; } }
            public SimulationCommand Command { get { return command; } }
        }

        private sealed class KeywordBinding
        {
            public string Keyword;
            public UnityEvent RecognitionEvent;
            public UnityAction Listener;
        }

        [Header("Keyword phrases")]
        [SerializeField] private KeywordMapping[] keywordMappings =
        {
            new KeywordMapping { keyword = "放大", command = SimulationCommand.ZoomIn },
            new KeywordMapping { keyword = "缩小", command = SimulationCommand.ZoomOut },
            new KeywordMapping { keyword = "左转", command = SimulationCommand.RotateLeft },
            new KeywordMapping { keyword = "右转", command = SimulationCommand.RotateRight },
            new KeywordMapping { keyword = "复位", command = SimulationCommand.Reset },
            new KeywordMapping { keyword = "撤销", command = SimulationCommand.Undo },
            new KeywordMapping { keyword = "确认", command = SimulationCommand.Confirm },
            new KeywordMapping { keyword = "取消", command = SimulationCommand.Cancel }
        };

        [Header("Availability")]
        [SerializeField, Min(0.25f)] private float retryIntervalSeconds = 2f;
        [SerializeField] private TMP_Text statusText;

        private readonly List<KeywordBinding> bindings = new List<KeywordBinding>();
        private IKeywordRecognitionSubsystem subsystem;
        private bool hasAvailableSubsystem;
        private float nextRetryTime;

        public bool IsAvailable
        {
            get
            {
                return isActiveAndEnabled && hasAvailableSubsystem &&
                    subsystem != null && subsystem.running && bindings.Count > 0;
            }
        }

        public string StatusMessage { get; private set; }
        public int RegisteredKeywordCount { get { return bindings.Count; } }

        public event Action<SimulationCommand> CommandRecognized;
        public event Action<bool, string> AvailabilityChanged;

        private void OnEnable()
        {
            nextRetryTime = 0f;
            TryAttach();
        }

        private void Update()
        {
            if (IsAvailable || Time.unscaledTime < nextRetryTime)
            {
                return;
            }

            retryIntervalSeconds = Mathf.Max(0.25f, retryIntervalSeconds);
            nextRetryTime = Time.unscaledTime + retryIntervalSeconds;
            TryAttach();
        }

        private void OnDisable()
        {
            DetachListeners();
            subsystem = null;
            hasAvailableSubsystem = false;
            SetStatus(false, "Keyword source is disabled.");
        }

        private void OnDestroy()
        {
            DetachListeners();
        }

        private void OnValidate()
        {
            retryIntervalSeconds = Mathf.Max(0.25f, retryIntervalSeconds);
        }

        private void TryAttach()
        {
            IKeywordRecognitionSubsystem candidate = XRSubsystemHelpers.KeywordRecognitionSubsystem;
            if (candidate == null || !candidate.running)
            {
                DetachListeners();
                subsystem = null;
                hasAvailableSubsystem = false;
                SetStatus(false, "MRTK keyword-recognition subsystem is not running.");
                return;
            }

            if (ReferenceEquals(candidate, subsystem) && bindings.Count > 0)
            {
                hasAvailableSubsystem = true;
                SetStatus(true, "Keyword recognition available (" + bindings.Count + " phrases).");
                return;
            }

            DetachListeners();
            subsystem = candidate;

            HashSet<string> uniqueKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> registrationErrors = new List<string>();

            if (keywordMappings != null)
            {
                for (int i = 0; i < keywordMappings.Length; i++)
                {
                    KeywordMapping mapping = keywordMappings[i];
                    if (mapping == null || string.IsNullOrWhiteSpace(mapping.Keyword))
                    {
                        registrationErrors.Add("mapping " + i + " is empty");
                        continue;
                    }

                    string keyword = mapping.Keyword.Trim();
                    if (!uniqueKeywords.Add(keyword))
                    {
                        registrationErrors.Add("duplicate phrase: " + keyword);
                        continue;
                    }

                    try
                    {
                        UnityEvent recognitionEvent = subsystem.CreateOrGetEventForKeyword(keyword);
                        if (recognitionEvent == null)
                        {
                            registrationErrors.Add("no event for phrase: " + keyword);
                            continue;
                        }

                        SimulationCommand command = mapping.Command;
                        UnityAction listener = delegate { OnKeywordRecognized(command); };
                        recognitionEvent.AddListener(listener);
                        bindings.Add(new KeywordBinding
                        {
                            Keyword = keyword,
                            RecognitionEvent = recognitionEvent,
                            Listener = listener
                        });
                    }
                    catch (Exception exception)
                    {
                        registrationErrors.Add(keyword + ": " + exception.GetType().Name);
                    }
                }
            }

            hasAvailableSubsystem = bindings.Count > 0 && subsystem.running;
            if (hasAvailableSubsystem)
            {
                string message = "Keyword recognition available (" + bindings.Count + " phrases).";
                if (registrationErrors.Count > 0)
                {
                    message += " Some phrases were skipped: " + string.Join(", ", registrationErrors.ToArray());
                }

                SetStatus(true, message);
            }
            else
            {
                string message = registrationErrors.Count > 0
                    ? "No keyword phrases could be registered: " + string.Join(", ", registrationErrors.ToArray())
                    : "No keyword phrases are configured.";
                SetStatus(false, message);
            }
        }

        private void DetachListeners()
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                KeywordBinding binding = bindings[i];
                if (binding.RecognitionEvent != null && binding.Listener != null)
                {
                    binding.RecognitionEvent.RemoveListener(binding.Listener);
                }
            }

            bindings.Clear();
        }

        private void OnKeywordRecognized(SimulationCommand command)
        {
            if (!IsAvailable)
            {
                return;
            }

            Action<SimulationCommand> handler = CommandRecognized;
            if (handler != null)
            {
                handler(command);
            }
        }

        private void SetStatus(bool available, string message)
        {
            bool changed = hasStatus != available || StatusMessage != message;
            hasStatus = available;
            StatusMessage = message;
            if (statusText != null)
            {
                statusText.text = "Voice: " + message;
            }

            if (changed)
            {
                Action<bool, string> handler = AvailabilityChanged;
                if (handler != null)
                {
                    handler(available, message);
                }
            }
        }

        private bool hasStatus;
    }
}
