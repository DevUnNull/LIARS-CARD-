// Phản ánh trạng thái lên animation và UI
using UnityEngine;

using System;
using UnityEngine;

namespace LiarsCard.Penalty
{
    public sealed class PenaltyPresenter : MonoBehaviour
    {
        [SerializeField] private string penaltyId;

        private IPenaltyStateSource stateSource;
        private PenaltyState? currentState;
        private long currentRevision = -1;

        public PenaltyState? CurrentState => currentState;

        public void Initialize(IPenaltyStateSource source)
        {
            if (source == null)
            {
                Debug.LogError("PenaltyPresenter: state source is null.", this);
                return;
            }

            if (stateSource != null)
            {
                stateSource.SnapshotReceived -= OnSnapshotReceived;
                stateSource.Unsubscribe();
            }

            stateSource = source;
            stateSource.SnapshotReceived += OnSnapshotReceived;
            stateSource.Subscribe();
        }

        private void OnEnable()
        {
            if (stateSource != null)
            {
                stateSource.SnapshotReceived += OnSnapshotReceived;
                stateSource.Subscribe();
            }
        }

        private void OnDisable()
        {
            if (stateSource != null)
            {
                stateSource.SnapshotReceived -= OnSnapshotReceived;
                stateSource.Unsubscribe();
            }
        }

        private void OnDestroy()
        {
            if (stateSource != null)
            {
                stateSource.SnapshotReceived -= OnSnapshotReceived;
                stateSource.Unsubscribe();
                stateSource = null;
            }
        }

        private void OnSnapshotReceived(PenaltySnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning(
                    "PenaltyPresenter: received null snapshot.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(snapshot.PenaltyId))
            {
                Debug.LogWarning(
                    "PenaltyPresenter: missing PenaltyId.", this);
                return;
            }

            if (!string.IsNullOrWhiteSpace(penaltyId) &&
                snapshot.PenaltyId != penaltyId)
            {
                return;
            }

            if (snapshot.Revision < 1)
            {
                Debug.LogWarning(
                    "PenaltyPresenter: invalid revision.", this);
                return;
            }

            if (!Enum.IsDefined(
                    typeof(PenaltyState), snapshot.State))
            {
                Debug.LogWarning(
                    "PenaltyPresenter: invalid state.", this);
                return;
            }

            if (currentState.HasValue)
            {
                if (snapshot.Revision < currentRevision)
                    return;

                if (snapshot.Revision == currentRevision)
                {
                    if (snapshot.State != currentState.Value)
                    {
                        Debug.LogError(
                            "PenaltyPresenter: conflicting snapshot " +
                            "with the same revision.", this);
                    }

                    return;
                }

                // Snapshot là ảnh chụp trạng thái hiện tại.
                // Có thể bỏ qua revision trung gian khi reconnect.
                if (!PenaltyStateMachine.CanTransition(
                        currentState.Value, snapshot.State))
                {
                    Debug.LogWarning(
                        "PenaltyPresenter: invalid state transition. " +
                        "Resynchronization is required.", this);
                    return;
                     
                }
            }

            penaltyId = snapshot.PenaltyId;
            currentRevision = snapshot.Revision;
            currentState = snapshot.State;

            RenderState(snapshot.State);
        }

        private void RenderState(PenaltyState state)
        {
            switch (state)
            {
                case PenaltyState.Pending:
                    ShowPending();
                    break;

                case PenaltyState.Animating:
                    PlayPenaltyAnimation();
                    break;

                case PenaltyState.Resolving:
                    ShowResolving();
                    break;

                case PenaltyState.Completed:
                    ShowCompleted();
                    break;
            }
        }

        private void ShowPending()
        {
            // TODO: Hiển thị trạng thái đang chờ.
        }

        private void PlayPenaltyAnimation()
        {
            // TODO: Phát animation hình phạt.
            // Không tự chuyển state khi animation kết thúc.
        }

        private void ShowResolving()
        {
            // TODO: Hiển thị trạng thái đang xử lý.
        }

        private void ShowCompleted()
        {
            // TODO: Hiển thị hình phạt hoàn tất.
        }
    }
}
