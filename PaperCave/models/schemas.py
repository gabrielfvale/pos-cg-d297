"""
models/schemas.py
Pydantic schemas for all agent outputs.

v4.1 changes (Card system):
  - GameObject / ObjectManifest / ReviewedManifest replaced by the Card system:
    Card, CardContent, CardStyleHint, AnimationFrame, CardManifest,
    ReviewedCardManifest.
  - Cards have two states (collapsed/expanded) and one of three contentTypes:
    figure, chart, animation.
  - ObjectScore / ReviewResult / ImplementationNotes kept (Reviewer + final output).
  - PaperProfile.gameStructure auto-set to "none" when implementsGame=False.
"""
from pydantic import BaseModel, Field, model_validator
from typing import Any, Dict, List, Literal, Optional


# ── Classifier output (v2 legacy) ─────────────────────────────────────────────

class PaperProfile(BaseModel):
    paperTitle: str
    domain: str
    centralContribution: Literal[
        "framework", "algorithm", "evaluation", "system", "survey", "game"
    ]
    hasImplementation: bool
    hasMeasurableMetrics: bool
    isAboutGames: bool
    implementsGame: bool
    gameStructure: Literal[
        "exploration", "sequential", "comparative", "parametric",
        "observational", "none"
    ]
    interactionPotential: Literal["high", "medium", "low"]
    keyConcepts: List[str] = Field(max_length=5)
    suggestedTemplate: Literal["T1", "T2", "T3", "T4", "T5"]
    templateRationale: str

    @model_validator(mode="after")
    def enforce_game_structure(self) -> "PaperProfile":
        if not self.implementsGame:
            self.gameStructure = "none"
        return self


# ── Designer output (v2 legacy) ────────────────────────────────────────────────

class NarrativeFrame(BaseModel):
    openingContext: str
    centralChallenge: str
    resolution: str


class Objective(BaseModel):
    index: int
    description: str
    mappedConcept: str = Field(description="which paper concept this step represents")
    completionTrigger: str = Field(description="condition that marks progress to the next step")


class KeyObject(BaseModel):
    type: str
    role: str
    parameters: Dict[str, Any] = Field(default_factory=dict)
    interactionNote: str = ""


class MediaWindowOutline(BaseModel):
    mode: Literal["image", "lottie", "chart"]
    contentDescription: str


class AreaOutline(BaseModel):
    areaId: str
    label: str
    purpose: str
    keyObjects: List[KeyObject]
    mediaWindows: List[MediaWindowOutline] = []


class DesignPlan(BaseModel):
    templateId: Literal["T1", "T2", "T3", "T4", "T5"]
    templateRationale: str
    perspective: Literal["first_person", "third_person", "top_down"]
    perspectiveRationale: str
    narrativeFrame: NarrativeFrame
    objectives: List[Objective] = Field(min_length=1, max_length=3)
    areaOutline: List[AreaOutline] = Field(min_length=1, max_length=3)
    designNotes: str = ""


# ── Image pipeline schemas (v3) ────────────────────────────────────────────────

class ImageContext(BaseModel):
    """Metadata of an image extracted from the PDF by the Reader."""
    raw_filename: str
    page: int
    caption: str = ""
    context_before: str = ""
    context_after: str = ""
    width_px: int = 0
    height_px: int = 0


class SelectedImage(BaseModel):
    """Image chosen by the Image Selector."""
    filename: str
    raw_filename: str
    caption: str = ""
    selection_justification: str


class ImageSelection(BaseModel):
    """Output of the Image Selector."""
    selected: List[SelectedImage] = Field(max_length=3)


class ImageInsight(BaseModel):
    """Vision Analyst output for one image."""
    filename: str
    description: str
    relevance: str
    mode: Literal["visual", "text_inferred"]


class ImageInsights(BaseModel):
    """Full Vision Analyst output."""
    insights: List[ImageInsight]


# ── Extractor output (v3, ex-Classifier) ──────────────────────────────────────

class ConceptualElement(BaseModel):
    name: str
    category: Literal[
        "problem", "method", "dataset", "metric", "result",
        "contribution", "limitation", "character", "artifact", "relation"
    ]
    description: str
    relevanceScore: int = Field(ge=1, le=10)
    justification: str


class ExtractionResult(BaseModel):
    paperTitle: str
    centralContribution: str
    elements: List[ConceptualElement] = Field(min_length=8, max_length=10)


# ── Card system schemas (v4.1, ex-Mapper output) ───────────────────────────────

class CardStyleHint(BaseModel):
    categoryColor: str = Field(description="Hex color for this category")
    colorName: str     = Field(description="Human-readable color name")


class AnimationFrame(BaseModel):
    index: int
    label: str       = Field(max_length=20)
    description: str = Field(max_length=120)
    assetReference: Optional[str] = None


class CardContent(BaseModel):
    """
    Union-style content block. Fields used depend on contentType:
      figure    → assetReference, caption, description
      chart     → chartType, title, description, data
      animation → description, frameCount, frames, transitionType, looping
    """
    # figure fields
    assetReference: Optional[str]  = None
    caption:        Optional[str]  = None

    # shared description
    description: str = ""

    # chart fields
    chartType: Optional[Literal["bar", "grouped_bar", "table", "comparison_table"]] = None
    title:     Optional[str]          = None
    data:      Optional[Dict[str, Any]] = None

    # animation fields
    frameCount:     Optional[int]   = None
    frames:         Optional[List[AnimationFrame]] = None
    transitionType: Optional[Literal["fade", "slide"]] = None
    looping:        Optional[bool]  = None

    @model_validator(mode="after")
    def validate_content_by_type(self) -> "CardContent":
        # Validation is soft — the Reviewer agent handles semantic validation.
        # We only enforce that description is always present.
        if not self.description:
            raise ValueError("CardContent.description must not be empty")
        return self


class Card(BaseModel):
    id:       str = Field(description="Unique card ID: card_01, card_02, ...")
    title:    str = Field(max_length=30, description="Shown in collapsed state")
    category: Literal[
        "contribution", "method", "problem", "metric", "result",
        "limitation", "dataset", "artifact", "relation"
    ]
    priority: Literal["primary", "secondary"] = Field(
        description="Exactly 1 card must be primary (the central contribution)"
    )
    summary: str = Field(
        max_length=80,
        description="Single line shown in collapsed state"
    )
    contentType: Literal["figure", "chart", "animation"]
    content: CardContent
    conceptualOrigin: str = Field(
        description="Specific section, figure, or table from the paper"
    )
    whyThisCard: str = Field(
        description="Why this concept was selected"
    )
    styleHint: CardStyleHint

    @model_validator(mode="after")
    def validate_figure_has_reference(self) -> "Card":
        if self.contentType == "figure" and not self.content.assetReference:
            raise ValueError(
                f"Card '{self.id}' has contentType='figure' but "
                f"content.assetReference is null. "
                f"Only use contentType='figure' when a FIG*.png exists."
            )
        return self

    @model_validator(mode="after")
    def validate_chart_has_data(self) -> "Card":
        if self.contentType == "chart" and not self.content.data:
            raise ValueError(
                f"Card '{self.id}' has contentType='chart' but "
                f"content.data is null. Include the actual paper values."
            )
        return self

    @model_validator(mode="after")
    def validate_animation_has_frames(self) -> "Card":
        if self.contentType == "animation":
            if not self.content.frames or len(self.content.frames) < 2:
                raise ValueError(
                    f"Card '{self.id}' has contentType='animation' but "
                    f"content.frames has fewer than 2 frames."
                )
        return self


class CardManifest(BaseModel):
    paperTitle:          str
    centralContribution: str
    cardCount:           int = Field(ge=2, le=20)
    cards: List[Card]

    @model_validator(mode="after")
    def validate_card_count_matches(self) -> "CardManifest":
        if len(self.cards) != self.cardCount:
            raise ValueError(
                f"cardCount={self.cardCount} but {len(self.cards)} cards provided"
            )
        return self

    @model_validator(mode="after")
    def validate_exactly_one_primary(self) -> "CardManifest":
        primaries = [c for c in self.cards if c.priority == "primary"]
        if len(primaries) != 1:
            raise ValueError(
                f"Exactly 1 card must have priority='primary'. "
                f"Found: {len(primaries)}"
            )
        return self


# ── Reviewer output (v3/v4) ────────────────────────────────────────────────────

class ObjectScore(BaseModel):
    suggestedName: str
    score: float = Field(ge=0.0, le=1.0)
    confidence: Literal["high", "medium", "low"]
    feedback: str


class ReviewResult(BaseModel):
    attempt: int
    approved: bool
    objectScores: List[ObjectScore]
    overallFeedback: str


class ImplementationNotes(BaseModel):
    """Implementation guidance for the Unity developer / Unity MCP."""
    paperFiguresPath: str = "Assets/PaperFigures/"
    assetsBasePath: str   = "Assets/SciFiPack/"
    styleGuide: str       = "See assets/visual_style.md — sci-fi educational aesthetic"
    sceneLayout: str      = (
        "Place the category='contribution' object as the central focal point. "
        "Arrange remaining objects in a semicircle with generous spacing. "
        "Scale contribution 1.5-2x others."
    )


class ReviewedCardManifest(BaseModel):
    """Final output of the Reviewer — best card manifest assembled from all attempts."""
    paperTitle:          str
    centralContribution: str
    cardCount:           int
    cards:               List[Card]
    objectScores:        List[ObjectScore]
    totalAttempts:       int
    assembledFromMultipleAttempts: bool
    implementationNotes: ImplementationNotes = Field(
        default_factory=ImplementationNotes
    )
