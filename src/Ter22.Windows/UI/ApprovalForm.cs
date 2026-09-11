namespace Ter22.Windows.UI;

internal sealed class ApprovalForm : Form
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public TaskCompletionSource<bool> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ApprovalForm()
    {
        Text = "Ter22 Remote — cerere de acces"; Width = 510; Height = 220;
        FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false;
        var label = new Label { Text = "Un client care deține codul tău solicită acces la monitoare, mouse și tastatură. Permiți această sesiune?",
            Dock = DockStyle.Fill, Padding = new Padding(20) };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(10), FlowDirection = FlowDirection.RightToLeft };
        var deny = new Button { Text = "Refuză", AutoSize = true }; var allow = new Button { Text = "Permite accesul", AutoSize = true };
        deny.Click += (_, _) => { Result.TrySetResult(false); Close(); };
        allow.Click += (_, _) => { Result.TrySetResult(true); Close(); };
        bar.Controls.AddRange([deny, allow]); Controls.Add(label); Controls.Add(bar);
        CancelButton = deny; FormClosed += (_, _) => Result.TrySetResult(false);
    }
}
