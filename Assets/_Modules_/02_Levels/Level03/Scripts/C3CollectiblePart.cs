using UnityEngine;

namespace C3TreasurePuzzle
{
    [RequireComponent(typeof(Collider))]
    public sealed class C3CollectiblePart : C3Interactable
    {
        [SerializeField] private string partId;
        [SerializeField] private string displayName;
        [SerializeField] private C3TreasureProgress progress;
        private bool collected;
        public string PartId => partId;
        public override string DisplayName => displayName;

        public void Configure(string id, string name, C3TreasureProgress owner)
        {
            partId = id;
            displayName = name;
            progress = owner;
        }

        private void Awake()
        {
            if (progress == null) progress = FindObjectOfType<C3TreasureProgress>();
        }

        public override bool CanInteract(GameObject actor) =>
            isActiveAndEnabled && !collected && progress != null && !progress.HasPart(partId);

        public override void Interact(GameObject actor)
        {
            if (!CanInteract(actor) || !progress.TryCollect(partId, displayName)) return;
            collected = true;
            gameObject.SetActive(false);
        }
    }
}
