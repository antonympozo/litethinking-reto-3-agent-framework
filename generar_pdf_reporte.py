from pathlib import Path
from reportlab.lib.pagesizes import letter
from reportlab.lib import colors
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, HRFlowable, PageBreak
)
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.pdfgen import canvas


class NumberedCanvas(canvas.Canvas):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self._saved_page_states)
        for state in self._saved_page_states:
            self.__dict__.update(state)
            self.draw_header_footer(num_pages)
            super().showPage()
        super().save()

    def draw_header_footer(self, page_count):
        self.saveState()
        self.setFont("Helvetica", 8)
        self.setFillColor(colors.HexColor("#64748B"))

        if self._pageNumber > 1:
            self.drawString(45, 755, "Reto 3 · Ingeniería de Agentes con Microsoft Agent Framework (.NET)")
            self.drawRightString(612 - 45, 755, "RETO-MAF-WORKFLOW-01")
            self.setStrokeColor(colors.HexColor("#CBD5E1"))
            self.setLineWidth(0.5)
            self.line(45, 749, 612 - 45, 749)

        self.setStrokeColor(colors.HexColor("#CBD5E1"))
        self.setLineWidth(0.5)
        self.line(45, 38, 612 - 45, 38)

        self.drawString(45, 26, "Alumno: Marco Antonio Pozo Espinoza | Microsoft Agent Framework .NET 10 + DeepSeek V3")
        page_text = f"Página {self._pageNumber} de {page_count}"
        self.drawRightString(612 - 45, 26, page_text)
        self.restoreState()


def generar_pdf(pdf_path: str):
    doc = SimpleDocTemplate(
        pdf_path,
        pagesize=letter,
        leftMargin=45,
        rightMargin=45,
        topMargin=42,
        bottomMargin=45
    )

    styles = getSampleStyleSheet()
    c_primary = colors.HexColor("#0B3954")
    c_accent = colors.HexColor("#087E8B")
    c_slate = colors.HexColor("#1E293B")
    c_border = colors.HexColor("#CBD5E1")
    c_bg_light = colors.HexColor("#F8FAFC")
    c_bg_subtle = colors.HexColor("#F1F5F9")

    title_style = ParagraphStyle(
        "DocTitle", parent=styles["Normal"], fontName="Helvetica-Bold",
        fontSize=14, leading=17, textColor=c_primary, spaceAfter=2
    )
    subtitle_style = ParagraphStyle(
        "DocSubTitle", parent=styles["Normal"], fontName="Helvetica",
        fontSize=8.5, leading=11.5, textColor=colors.HexColor("#475569"), spaceAfter=6
    )
    h1_style = ParagraphStyle(
        "Heading1_Custom", parent=styles["Normal"], fontName="Helvetica-Bold",
        fontSize=10.5, leading=13.5, textColor=c_primary, spaceBefore=8, spaceAfter=4
    )
    h2_style = ParagraphStyle(
        "Heading2_Custom", parent=styles["Normal"], fontName="Helvetica-Bold",
        fontSize=9, leading=12, textColor=c_accent, spaceBefore=6, spaceAfter=6
    )
    body_style = ParagraphStyle(
        "Body_Custom", parent=styles["Normal"], fontName="Helvetica",
        fontSize=8.2, leading=11.2, textColor=c_slate, spaceAfter=5
    )
    code_style = ParagraphStyle(
        "Code_Custom", parent=styles["Normal"], fontName="Courier",
        fontSize=7.2, leading=9.4, textColor=colors.HexColor("#0F172A"),
        backColor=c_bg_subtle, borderPadding=4, spaceBefore=5, spaceAfter=6
    )
    th_style = ParagraphStyle(
        "TH", parent=styles["Normal"], fontName="Helvetica-Bold",
        fontSize=7.8, leading=10, textColor=colors.white
    )
    td_style = ParagraphStyle(
        "TD", parent=styles["Normal"], fontName="Helvetica",
        fontSize=7.6, leading=9.8, textColor=c_slate
    )

    story = []

    story.append(Paragraph("Certificación 3 · Ingeniería de Agentes con Microsoft Agent Framework", title_style))
    story.append(Paragraph(
        "<b>ID del Reto:</b> RETO-MAF-WORKFLOW-01 &nbsp;|&nbsp; "
        "<b>Estudiante:</b> Marco Antonio Pozo Espinoza &nbsp;|&nbsp; "
        "<b>Stack:</b> .NET SDK 10.0.301 · Microsoft.Agents.AI.Workflows 1.27.0 · DeepSeek V3 (deepseek-chat)",
        subtitle_style
    ))
    story.append(HRFlowable(width="100%", thickness=1, color=c_primary, spaceAfter=6))

    story.append(Paragraph("1. Verificación del Entorno y Cumplimiento de Criterios", h1_style))
    story.append(Paragraph(
        "Se verificaron las herramientas locales (<b>dotnet --version: 10.0.301</b>, <b>git --version: 2.50.1</b>, "
        "<b>az --version: 2.71.0</b>), se clonó el módulo <b>07.Workflow</b> del repositorio oficial "
        "<i>microsoft/Agent-Framework-Samples</i> y se configuraron las credenciales en <b>.env</b> (excluido en "
        "<i>.gitignore</i> y eliminado al finalizar conforme al paso 2.9).",
        body_style
    ))

    criterios_data = [
        [Paragraph("#", th_style), Paragraph("Caso / Criterio de Aceptación", th_style),
         Paragraph("Patrón de Grafo (.NET C#)", th_style), Paragraph("Resultado / Evidencia", th_style)],
        [Paragraph("1-2", td_style), Paragraph("Entorno y credenciales seguras (.env)", td_style),
         Paragraph("DotNetEnv + .gitignore + limpieza", td_style), Paragraph("Verificado (.NET 10.0.301, 0 secretos expuestos)", td_style)],
        [Paragraph("3", td_style), Paragraph("Caso 1: Secuencial Básico (París)", td_style),
         Paragraph("FrontDesk -> Concierge", td_style), Paragraph("FrontDesk recomienda Torre Eiffel; Concierge sugiere alternativa local", td_style)],
        [Paragraph("9", td_style), Paragraph("Paso 10: Extensión Caso 1 + Translator", td_style),
         Paragraph("FrontDesk -> Concierge -> Translator", td_style), Paragraph("Tercer agente traduce la revisión completa al español conservando el tono", td_style)],
        [Paragraph("4", td_style), Paragraph("Caso 2: Secuencial Multi-paso Multimodal", td_style),
         Paragraph("Sales-Agent -> Price-Agent -> Quote-Agent", td_style), Paragraph("Procesa imgs/home.png (10 muebles) y genera cotización Markdown ($2,834.10)", td_style)],
        [Paragraph("5", td_style), Paragraph("Caso 3: Concurrente (Seattle in December)", td_style),
         Paragraph("AddFanOutEdge + AddFanInBarrierEdge", td_style), Paragraph("Researcher-Agent y Plan-Agent ejecutan en paralelo en SuperStep 1", td_style)],
        [Paragraph("6-7", td_style), Paragraph("Caso 4: Condicional — Rama Aprobada ('Yes')", td_style),
         Paragraph("Draft -> Reviewer -> PublishExecutor", td_style), Paragraph("Tutorial de 531 palabras (>200) guardado en 20250521034512.md", td_style)],
        [Paragraph("8", td_style), Paragraph("Caso 4: Condicional — Rama Rechazada ('No')", td_style),
         Paragraph("Draft -> Reviewer -> SendReviewExecutor", td_style), Paragraph("Borrador de 27 palabras (<200) rechazado: 'Content is too short'", td_style)],
    ]
    t = Table(criterios_data, colWidths=[24, 150, 155, 193])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), c_primary),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, c_bg_light]),
        ("GRID", (0, 0), (-1, -1), 0.4, c_border),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 3.5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 3.5),
    ]))
    story.append(t)
    story.append(Spacer(1, 5))

    story.append(Paragraph("2. Evidencia de Ejecución: Casos 1 (Original + Translator), 2 y 3", h1_style))

    story.append(Paragraph("Caso 1 Original y Extendido con Agente Translator (Paso 10)", h2_style))
    story.append(Paragraph(
        "Grafo extendido: <b>WorkflowBuilder(frontDeskAgent).AddEdge(frontDeskAgent, reviewerAgent).AddEdge(reviewerAgent, translatorAgent).Build()</b>",
        body_style
    ))
    story.append(Paragraph(
        "[FrontDesk]: Visit the Eiffel Tower at sunset for the best views.<br/>"
        "[Concierge]: While the Eiffel Tower at sunset is undeniably beautiful, it's also one of the most heavily touristed experiences in Paris... "
        "As a concierge focused on authentic, local experiences, I'd say this recommendation is **not approved** as the *best* non-touristy option...<br/>"
        "[Translator (Salida Final en Español)]: Aunque la Torre Eiffel al atardecer es innegablemente hermosa, también es una de las experiencias más "
        "masificadas por el turismo en París: largas colas, multitudes y una vista que compartirás con miles de personas. Como conserje centrado en "
        "experiencias auténticas y locales, diría que esta recomendación **no está aprobada** como la *mejor* opción no turística. Para refinarla: "
        "considera orientar al viajero hacia experiencias que los locales realmente frecuentan — rincones más pequeños de barrio, miradores menos "
        "conocidos o espacios culturales fuera del circuito turístico principal...",
        code_style
    ))

    story.append(Paragraph("Caso 2: Cadena Secuencial Multi-paso (Sales-Agent -> Price-Agent -> Quote-Agent)", h2_style))
    story.append(Paragraph(
        "A partir de <i>DataContent(imageBytes, 'image/png')</i> de <i>imgs/home.png</i>, Sales-Agent identificó los 10 muebles, "
        "Price-Agent calculó los niveles económico ($1,725), medio ($3,890) y premium ($8,620), y Quote-Agent emitió la cotización en Markdown:",
        body_style
    ))
    story.append(Paragraph(
        "# FURNITURE PURCHASE QUOTATION (Ref: Q-LR-2024-01)<br/>"
        "1. 3-Seater White Fabric Sofa ($899) | 2. Navy Wingback Accent Armchair + Footstool ($549) | 3. Triangular Light-Oak Coffee Table ($329)<br/>"
        "4. Low-Profile Light-Wood TV Media Console ($459) | 5. Round White Pedestal Side Table ($149) | 6. Black-Metal Frame End Table ($129)<br/>"
        "7. Black 8-Globe Sputnik Chandelier ($219) | 8. Hanging Glass Pendant Lights ($139) | 9. Framed Wall Art x2 ($158) | 10. Curtains ($119)<br/>"
        "Subtotal: $3,149.00 | Bundle Discount (10%): -$314.90 | <b>Estimated Total: $2,834.10 USD</b>",
        code_style
    ))

    story.append(Paragraph("Caso 3: Workflow Concurrente Fan-Out / Fan-In ('Plan a trip to Seattle in December')", h2_style))
    story.append(Paragraph(
        "[Event 2] SuperStepStartedEvent(Step = 0) -> ExecutorInvokedEvent(ConcurrentStartExecutor)<br/>"
        "[Event 6] SuperStepStartedEvent(Step = 1) -> ExecutorInvokedEvent(Plan_Agent) &amp; ExecutorInvokedEvent(Researcher_Agent) [PARALELO]<br/>"
        "[ConcurrentAggregationExecutor] Received 2 messages -> Yielding output with 2 messages -> WorkflowOutputEvent (Itinerario de 5 días en Seattle).",
        code_style
    ))

    story.append(PageBreak())

    story.append(Paragraph("3. Evidencia del Caso 4: Workflow Condicional (Rama Aprobada y Rama Rechazada)", h1_style))
    story.append(Paragraph(
        "El grafo condicional conecta <b>DraftExecutor</b> con <b>ContentReviewExecutor</b> y evalúa dos aristas condicionales mediante "
        "<b>GetCondition(expectedResult: 'Yes')</b> hacia <b>PublishExecutor</b> y <b>GetCondition(expectedResult: 'No')</b> hacia <b>SendReviewExecutor</b>.",
        body_style
    ))

    story.append(Paragraph("A. Rama Aprobada ('Yes' — Tutorial Técnico > 200 palabras guardado en Markdown)", h2_style))
    story.append(Paragraph(
        "Starting workflow... (Mode: FULL TUTORIAL -> Expected 'Yes' / Approved Branch)<br/>"
        "DraftExecutor generated content (531 words): # Introduction to AI Agents and Microsoft's Agent Ecosystem...<br/>"
        "ContentReviewExecutor .......loading<br/>"
        "ContentReviewExecutor review result: Yes, reason: The content is good<br/>"
        "PublishExecutor .......loading<br/>"
        "[Tool:SaveMarkdownFile] Saved Markdown file: .../04.dotnet-agent-framework-workflow-msfoundry-condition/20250521034512.md (4055 chars)<br/>"
        "=== WorkflowOutputEvent ===<br/>"
        "Publishing result: The tutorial draft has been saved successfully as **20250521034512.md**.",
        code_style
    ))

    story.append(Paragraph("B. Rama Rechazada ('No' — Borrador corto < 200 palabras sin invocar a Publisher-Agent)", h2_style))
    story.append(Paragraph(
        "Starting workflow... (Mode: SHORT DRAFT -> Expected 'No' / Rejected Branch)<br/>"
        "DraftExecutor generated content (27 words): AI agents are autonomous software systems that perceive their environment, make decisions, and take actions to achieve goals. They combine reasoning, tools, and memory to work independently.<br/>"
        "ContentReviewExecutor .......loading<br/>"
        "ContentReviewExecutor review result: No, reason: Content is too short<br/>"
        "=== WorkflowOutputEvent ===<br/>"
        "Draft content needs revision: Content is too short",
        code_style
    ))

    story.append(Paragraph("4. Respuestas a las Preguntas Conceptuales (Criterio 10)", h1_style))

    qa_data = [
        [Paragraph("Pregunta Conceptual", th_style), Paragraph("Respuesta Técnica", th_style)],
        [Paragraph("<b>1. ¿Qué es un executor?</b>", td_style),
         Paragraph(
             "Es la unidad fundamental de procesamiento dentro del grafo de un workflow en <i>Microsoft.Agents.AI.Workflows</i>. "
             "Recibe mensajes tipados mediante manejadores <i>[MessageHandler]</i>, ejecuta lógica o invoca un <i>AIAgent</i> "
             "(que es en sí mismo un tipo especializado de executor) y encamina mensajes al siguiente paso o emite salidas con <i>YieldOutputAsync</i>.",
             td_style
         )],
        [Paragraph("<b>2. ¿Qué diferencia hay entre un edge simple y uno condicional?</b>", td_style),
         Paragraph(
             "Un <b>edge simple</b> (<i>AddEdge(origen, destino)</i>) transfiere siempre e incondicionalmente la salida del nodo origen al destino. "
             "Un <b>edge condicional</b> (<i>AddEdge(origen, destino, condition: Func&lt;object?, bool&gt;)</i>) evalúa un predicado booleano en tiempo "
             "de ejecución sobre el mensaje emitido y solo activa la ruta si la condición devuelve <i>true</i> (ej. <i>review.Result == 'Yes'</i> vs <i>'No'</i>).",
             td_style
         )],
        [Paragraph("<b>3. ¿Qué hacen fan-out y fan-in?</b>", td_style),
         Paragraph(
             "<b>Fan-out</b> (<i>AddFanOutEdge</i>) difunde un mensaje desde un executor inicial hacia múltiples agentes en paralelo dentro del mismo "
             "<i>SuperStep</i> para ejecutar tareas concurrentes. <b>Fan-in</b> (<i>AddFanInEdge / AddFanInBarrierEdge</i>) actúa como barrera de "
             "sincronización y convergencia, recolectando las respuestas de todos los agentes paralelos en un executor agregador para emitir una salida unificada.",
             td_style
         )],
    ]
    t_qa = Table(qa_data, colWidths=[145, 377])
    t_qa.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), c_primary),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, c_bg_light]),
        ("GRID", (0, 0), (-1, -1), 0.4, c_border),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
    ]))
    story.append(t_qa)

    doc.build(story, canvasmaker=NumberedCanvas)


if __name__ == "__main__":
    out_pdf = str(Path(__file__).parent / "REPORTE_EXAMEN_FINAL_RETO_3.pdf")
    generar_pdf(out_pdf)
    print("PDF generado en:", out_pdf)
