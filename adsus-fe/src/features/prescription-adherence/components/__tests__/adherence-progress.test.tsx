import { render, screen } from "@testing-library/react";

import { AdherenceProgress } from "../adherence-progress";

describe("AdherenceProgress", () => {
  it("renders the percentage label", () => {
    render(<AdherenceProgress percent={80} level="good" label="Tuân thủ" />);
    expect(screen.getByText("Tuân thủ")).toBeInTheDocument();
    expect(screen.getByText("80%")).toBeInTheDocument();
  });

  it("clamps out-of-range values to [0, 100]", () => {
    const { rerender } = render(<AdherenceProgress percent={150} level="good" />);
    expect(screen.getByText("100%")).toBeInTheDocument();
    rerender(<AdherenceProgress percent={-10} level="poor" />);
    expect(screen.getByText("0%")).toBeInTheDocument();
  });

  it("uses level color class", () => {
    const { container } = render(<AdherenceProgress percent={50} level="warning" />);
    const bar = container.querySelector("div.h-full");
    expect(bar).toHaveClass("bg-amber-500");
  });
});
